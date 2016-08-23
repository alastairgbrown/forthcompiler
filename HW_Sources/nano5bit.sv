module nano5bit #
(
            parameter                  WIDTHIA = 12,
            parameter                  WIDTHID = 32,
            parameter                  WIDTHDA = 12,
            parameter                  WIDTHDD = 32,
            parameter                  STKBASE = 'h3e0
)
(
   input    logic                      clock,   // all inputs synchronous to this clock
   input    logic                      sreset,
   output   logic [WIDTHIA-1:0]        i_address,
   input    logic [WIDTHID-1:0]        i_readdata,
   output   logic                      i_read,
   input    logic                      i_waitrequest,
   output   logic [WIDTHDA-1:0]        d_address,
   output   logic [WIDTHDD-1:0]        d_writedata,
   input    logic [WIDTHDD-1:0]        d_readdata,
   output   logic                      d_read,
   output   logic                      d_write,
   input    logic                      d_waitrequest,
   input    logic                      irq
);
            localparam                 PCRESET = 'h4;
            localparam                 IRQADDR = 'h0;
            localparam                 WIDTHS = 5;
            localparam                 OPCODES = (WIDTHID / 5);
            localparam                 WIDTHI = (OPCODES * 5);
            localparam                 WIDTHO = $clog2(OPCODES);
            localparam                 ZERO = {WIDTHDD+WIDTHID{1'b0}};
            localparam                 ONE = {ZERO, 1'b1};
            localparam                 DONTCARE = {WIDTHDD+WIDTHID{1'bx}};
            integer                    clock_count, opcode_count;
   enum     logic [1:0]                {FETCH, EXECUTE} fsm;
            logic [WIDTHIA-1:0]        pc, next_pc;
            logic [WIDTHDD-1:0]        top, next, alu_logic, mlt;
            logic [WIDTHDD+1:0]        alu_adder;
            logic [WIDTHS-1:0]         sp;
            logic [OPCODES-1:0][4:0]   ir;
            logic [WIDTHO-1:0]         slot, next_slot;
            logic [4:0]                instr;
            logic [1:0]                sf;
            logic                      cf, ff, pf, irqf;
            logic                      irq_event, immediate, last_slot, top_zero, alu_cout;
            wire                       OP_PFX = ~instr[4];
            wire                       OP_LDW = (instr == 5'h10);
            wire                       OP_STW = (instr == 5'h11);
            wire                       OP_PSH = (instr == 5'h12);
            wire                       OP_POP = (instr == 5'h13);
            wire                       OP_SWP = (instr == 5'h14);
            wire                       OP_JNZ = (instr == 5'h15);
            wire                       OP_JSR = (instr == 5'h16);
            wire                       OP_ADD = (instr == 5'h17);
            wire                       OP_ADC = (instr == 5'h18);
            wire                       OP_SUB = (instr == 5'h19);
            wire                       OP_AND = (instr == 5'h1a);
            wire                       OP_IOR = (instr == 5'h1b);
            wire                       OP_XOR = (instr == 5'h1c);
            wire                       OP_MLT = (instr == 5'h1d);
            wire                       OP_LSR = (instr == 5'h1e);
            wire                       OP_ZEQ = (instr == 5'h1f);
   initial begin
      clock_count = 0;
      opcode_count = 0;
   end
   always_comb begin
      irq_event = ~irqf & irq & ~ff & ~pf & ~|sf;
      instr = ir[slot];
      last_slot = (slot == (OPCODES - 1));
      next_pc = pc + last_slot;
      next_slot = last_slot ? ZERO[WIDTHO-1:0] : (slot + ONE[WIDTHO-1:0]);
      top_zero = ~|top;
      mlt = next[WIDTHDD/2-1:0] * top[WIDTHDD/2-1:0];
      immediate = OP_SWP | OP_JSR | OP_PFX | OP_LSR | OP_ZEQ;
      i_address = pc;
      i_read = 1'b0;
      d_address = top[WIDTHDA-1:0];
      d_writedata = next;
      d_read = 1'b0;
      d_write = 1'b0;
      case (fsm)
         FETCH : begin
            d_address = {STKBASE[WIDTHDA-1:WIDTHS], sp + ONE[WIDTHS-1:0]};
            if (irq_event) begin
               d_write = 1'b1;
            end
            else begin
               i_read = 1'b1;
            end
         end
         EXECUTE : begin
            d_read = OP_LDW | OP_POP | OP_JNZ | OP_ADD | OP_ADC | OP_SUB | OP_AND | OP_IOR | OP_XOR | OP_MLT;
            d_write = OP_STW | OP_PSH;
            if (OP_POP|OP_PSH|OP_JNZ|OP_ADD|OP_ADC|OP_SUB|OP_AND|OP_IOR|OP_XOR) begin
               d_address = {STKBASE[WIDTHDA-1:WIDTHS], sp + OP_PSH};
            end
         end
      endcase
   end
   always_ff @ (posedge clock) begin
      alu_adder = {1'b0, next, 1'b1} + {1'b0, top ^ {WIDTHDD{OP_SUB}}, (cf & OP_ADC) | OP_SUB};
      alu_logic = {WIDTHDD{top_zero}};
      alu_cout = cf;
      if (OP_AND) alu_logic = next & top;
      if (OP_IOR) alu_logic = next | top;
      if (OP_XOR) alu_logic = next ^ top;
      if (OP_MLT) alu_logic = mlt;
      if (OP_LSR) begin
         alu_logic = {1'b0, top[WIDTHDD-1:1]};
         alu_cout = top[0];
      end
   end
   always_ff @ (posedge clock) begin
      clock_count++;
      if (sreset) begin
         ir <= DONTCARE[WIDTHI-1:0];
         top <= DONTCARE[WIDTHDD-1:0];
         next <= DONTCARE[WIDTHDD-1:0];
         sp <= 0;
         ff <= 0;
         cf <= 0;
         irqf <= 1'b0;
         pf <= 1'b0;
         pc <= PCRESET[WIDTHIA-1:0];
         slot <= 0;
         fsm <= FETCH;
      end
      else begin
         case (fsm)
            FETCH : begin
               ir <= i_readdata[WIDTHI-1:0];
               if (irq_event) begin
                  if (~d_waitrequest) begin
                     irqf <= 1'b1;
                     top <= {pc, slot};
                     next <= top;
                     pc <= IRQADDR[WIDTHIA-1:0];
                     slot <= ZERO[WIDTHO-1:0];
                     sp <= sp + ONE[WIDTHS-1:0];
                  end
               end
               else begin
                  ff <= 1'b1;
                  if (~i_waitrequest) begin
                     fsm <= EXECUTE;
                  end
               end
            end
            EXECUTE : begin
               ff <= 1'b0;
               if (immediate | ((d_read | d_write) & ~d_waitrequest)) begin
                  opcode_count++;
                  pf <= OP_PFX;
                  sf <= OP_SWP ? {sf[0], 1'b1} : 2'b00;
                  if (OP_PFX) begin
                     if (pf) begin
                        top <= {top[WIDTHDD-5:0], instr[3:0]};
                     end
                     else begin
                        top <= instr[3:0];
                     end
                  end
                  if (OP_LDW) begin
                     top <= d_readdata;
                  end
                  if (OP_JSR) begin
                     top <= {next_pc, next_slot};
                  end
                  if (OP_PSH) begin
                     sp <= sp + ONE[WIDTHS-1:0];
                     next <= top;
                  end
                  if (OP_POP|OP_JNZ|OP_ADD|OP_ADC|OP_SUB|OP_AND|OP_IOR|OP_XOR) begin
                     sp <= sp - ONE[WIDTHS-1:0];
                     next <= d_readdata;
                  end
                  if (OP_POP|OP_JNZ) begin
                     top <= next;
                  end
                  if (OP_AND|OP_IOR|OP_XOR|OP_LSR|OP_ZEQ|OP_MLT) begin
                     top <= alu_logic;
                     cf <= alu_cout;
                  end
                  if (OP_ADD|OP_ADC|OP_SUB) begin
                     top <= alu_adder[WIDTHDD:1];
                     cf <= alu_adder[WIDTHDD+1] ^ OP_SUB; // invery carry out for subtract
                  end
                  if (OP_JSR | (OP_JNZ & ~top_zero)) begin
                     irqf <= sf[1] ? 1'b0 : irqf;
                     pc <= top[WIDTHIA+WIDTHO-1:WIDTHO];
                     slot <= top[WIDTHO-1:0];
                     fsm <= FETCH;
                  end
                  else begin
                     if (last_slot) begin
                        fsm <= FETCH;
                     end
                     slot <= next_slot;
                     pc <= next_pc;
                  end
               end
            end
         endcase
      end
   end
endmodule
