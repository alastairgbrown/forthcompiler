module nano2a #
(
            parameter                  ARCH = "SMALL",   // SMALL MEDIUM LARGE
            parameter                  WIDTHIA = 10,
            parameter                  WIDTHID = 32,
            parameter                  WIDTHDA = 9,
            parameter                  WIDTHDD = 32,
            parameter                  WIDTHS = 5,
            parameter                  STKBASE = 'h3e0,
            parameter                  RSTKBASE = 'h3c0,
				parameter						PCRESET = 'h4,
				parameter						IRQADDR = 'h0,
            parameter                  WIDTHQ = 2
)
(
   input    logic                      clock,   // all inputs synchronous to this clock
   input    logic                      sreset,
   output   logic [WIDTHIA-1:0]        i_address,
   input    logic [WIDTHID-1:0]        i_readdata,
   output   logic                      i_read,
   input    logic                      i_waitrequest,
   input    logic                      i_readdatavalid,
   output   logic [WIDTHDA-1:0]        d_address,
   output   logic [WIDTHDD-1:0]        d_writedata,
   input    logic [WIDTHDD-1:0]        d_readdata,
   output   logic                      d_read,
   output   logic                      d_write,
   input    logic                      d_waitrequest,
   input    logic                      d_readdatavalid,
   input    logic [WIDTHQ-1:0]         irq
);
            localparam                 OPCODEMASK = (ARCH == "SMALL") ? 32'hfff :
                                          (ARCH == "MEDIUM") ? 32'h1ffff : 32'hffffff;
            localparam                 OPCODES = (WIDTHID / 5);
            localparam                 WIDTHI = (OPCODES * 5);
            localparam                 WIDTHO = $clog2(OPCODES);
            localparam                 ZERO = {WIDTHDD*2{1'b0}};
            localparam                 ONE = {ZERO, 1'b1};
            localparam                 DONTCARE = {WIDTHDD*2{1'bz}};
   enum     logic [1:0]                {FETCH, EXECUTE} fsm;
            logic [WIDTHIA-1:0]        pc;
            logic [WIDTHDD-1:0]        top, next, alu_result;
            logic [WIDTHS-1:0]         sp, rp;
            logic [OPCODES-1:0][4:0]   ir;
            logic [WIDTHO-1:0]         slot;
            logic [1:0]                sf;
            logic                      irq_reg;
            logic                      cf, ff, rf, pf, irqf, ef, alu_cout;
            wire [4:0]                 instr = ir[slot];
            wire                       OP_PFX = ~instr[4] & ~ef;  // cannot mask this away
            wire                       OP_LDW = (instr == 5'h10) & ~ef & OPCODEMASK[0];
            wire                       OP_STW = (instr == 5'h11) & ~ef & OPCODEMASK[1];
            wire                       OP_PSH = (instr == 5'h12) & ~ef & OPCODEMASK[2];
            wire                       OP_POP = (instr == 5'h13) & ~ef & OPCODEMASK[3];
            wire                       OP_SWP = (instr == 5'h14) & ~ef & OPCODEMASK[4];
            wire                       OP_JNZ = (instr == 5'h15) & ~ef & OPCODEMASK[5];
            wire                       OP_JSR = (instr == 5'h16) & ~ef & OPCODEMASK[6];
            wire                       OP_ADC = (instr == 5'h17) & ~ef & OPCODEMASK[7];
            wire                       OP_AND = (instr == 5'h18) & ~ef & OPCODEMASK[8];
            wire                       OP_XOR = (instr == 5'h19) & ~ef & OPCODEMASK[9];
            wire                       OP_LSR = (instr == 5'h1a) & ~ef & OPCODEMASK[10];
            wire                       OP_ZEQ = (instr == 5'h1b) & ~ef & OPCODEMASK[11];
            wire                       OP_ADD = (instr == 5'h1c) & ~ef & OPCODEMASK[12];
            wire                       OP_SUB = (instr == 5'h1d) & ~ef & OPCODEMASK[13];
            wire                       OP_IOR = (instr == 5'h1e) & ~ef & OPCODEMASK[14];
            wire                       OP_EXT = (instr == 5'h1f) & ~ef & OPCODEMASK[15];
            wire                       OP_MLT = (instr == 5'h00) & ef & OPCODEMASK[16];
            wire                       OP_RTO = (instr == 5'h01) & ef & OPCODEMASK[17];
            wire                       OP_TOR = (instr == 5'h02) & ef & OPCODEMASK[18];
            wire                       OP_LSP = (instr == 4'h03) & ef & OPCODEMASK[19];
            wire                       OP_LRP = (instr == 4'h04) & ef & OPCODEMASK[20];
            wire                       irq_event = irq_reg & ~irqf & ~ff & ~pf & ~(|sf) & ~ef;
            wire                       immediate = OP_PFX | OP_SWP | OP_LSP | OP_LRP | OP_JSR | OP_LSR | OP_ZEQ | OP_EXT;
            wire                       alu_2data = OP_ADD | OP_ADC | OP_SUB | OP_AND | OP_IOR | OP_XOR | OP_MLT;
            wire                       top_zero = ~|top;
            wire  [WIDTHDD*2-1:0]      mult_result = next[WIDTHDD/2-1:0] * top[WIDTHDD/2-1:0];
            wire  [WIDTHDD+1:0]        alu_adder = {1'b0, next, 1'b1} + {1'b0, top ^ {WIDTHDD{OP_SUB}}, ((cf & OP_ADC) | OP_SUB)};
            wire  [WIDTHDA-1:0]        stack_base = ((fsm == EXECUTE) && (OP_RTO | OP_TOR)) ? {RSTKBASE[WIDTHDA-1:WIDTHS], rp} : {STKBASE[WIDTHDA-1:WIDTHS], sp};
            wire  [WIDTHS-1:0]         prev_stack = stack_base[WIDTHS-1:0] - ONE[WIDTHS-1:0];
            wire  [WIDTHS-1:0]         next_stack = stack_base[WIDTHS-1:0] + ONE[WIDTHS-1:0];
            wire                       last_slot = (slot >= (OPCODES - 1));
            wire  [WIDTHO-1:0]         next_slot = last_slot ? ZERO[WIDTHO-1:0] : (slot + ONE[WIDTHO-1:0]);
            wire  [WIDTHIA-1:0]        next_pc = pc + last_slot;
            wire                       read_opcode = OP_LDW | OP_JNZ | OP_POP | OP_RTO | alu_2data;
            wire                       write_opcode = OP_STW | OP_PSH | OP_TOR;
            assign                     i_address = pc;
   
   always_comb begin
      alu_cout = cf;
      alu_result = {WIDTHDD{top_zero}};
      if (OP_ADD | OP_ADC | OP_SUB) {alu_cout, alu_result} = alu_adder[WIDTHDD+1:1];
      if (OP_AND) alu_result = next & top;
      if (OP_IOR) alu_result = next | top;
      if (OP_XOR) alu_result = next ^ top;
      if (OP_LSR) {alu_result, alu_cout} = {1'b0, top};
      if (OP_LSP) alu_result = sp;
      if (OP_LRP) alu_result = rp;
      if (OP_MLT) alu_result = mult_result[WIDTHDD-1:0];
   end
   always_ff @ (posedge clock) begin
      if (sreset) begin
         i_read <= 1'b0;
         d_read <= 1'b0;
         d_write <= 1'b0;
         pc <= PCRESET[WIDTHIA-1:0];
         ff <= 1'b0;
         irqf <= 1'b0;
         sf <= 2'b00;
         pf <= 1'b0;
         ef <= 1'b0;
         rf <= 1'b0;
         slot <= ZERO[WIDTHO-1:0];
         sp <= ZERO[WIDTHS-1:0];
         rp <= ZERO[WIDTHS-1:0];
         top <= DONTCARE[WIDTHDD-1:0];
         next <= DONTCARE[WIDTHDD-1:0];
         ir <= DONTCARE[WIDTHI-1:0];
         d_writedata <= DONTCARE[WIDTHDD-1:0];
         cf <= 1'bx;
         irq_reg <= 1'b0;
         fsm <= FETCH;
      end
      else begin
         irq_reg <= |irq;
         d_writedata <= ((fsm == EXECUTE) && OP_TOR) ? top : next;
         if (i_readdatavalid) begin
            ir <= i_readdata[WIDTHI-1:0];
         end
         case (fsm)
            FETCH : begin
               d_address <= stack_base + ONE[WIDTHS-1:0];
               if (irq_event) begin
                  d_write <= d_write ? d_waitrequest : 1'b1;
                  if (d_write & ~d_waitrequest) begin
                     irqf <= 1'b1;
                     top <= {pc, slot};
                     next <= top;
                     sp <= next_stack;
                     slot <= ZERO[WIDTHO-1:0];
                     pc <= IRQADDR[WIDTHIA-1:0];
                  end
               end
               else begin
                  ff <= 1'b1;
                  if (i_read) begin
                     if (~i_waitrequest) begin
                        i_read <= 1'b0;
                     end
                  end
                  else begin
                     i_read <= ~ff;
                  end
                  if (i_readdatavalid) begin
                     fsm <= EXECUTE;
                  end
               end
            end
            EXECUTE : begin
               ff <= 1'b0;
               d_address <= (OP_LDW | OP_STW) ? top[WIDTHDA-1:0] : (stack_base + (OP_PSH | OP_TOR));
               ef <= OP_EXT;
               pf <= OP_PFX;
               if (immediate | d_readdatavalid | (d_write & ~d_waitrequest)) begin
                  d_write <= 1'b0;
                  rf <= 1'b0;
                  sf <= OP_SWP ? {sf[0], 1'b1} : 2'b00;
                  if (OP_PFX) top <= pf ? {top[WIDTHDD-5:0], instr[3:0]} : {{WIDTHDD-5{instr[3]}}, instr[3:0]};
                  if (OP_LDW) top <= d_readdata;
                  if (OP_PSH) begin
                     next <= top;
                     sp <= next_stack;//d_address[WIDTHS-1:0];
                  end
                  if (OP_POP | OP_JNZ | alu_2data) begin
                     top <= alu_2data ? alu_result : next;
                     next <= d_readdata;
                     sp <= prev_stack;
                  end
                  if (OP_SWP) begin
                     top <= next;
                     next <= top;
                  end
                  if (OP_JSR) top <= {next_pc, next_slot};
                  if (OP_RTO) begin
                     top <= d_readdata;
                     rp <= prev_stack;
                  end
                  if (OP_TOR) rp <= d_address[WIDTHS-1:0];
                  if (alu_2data | OP_LSR | OP_ZEQ | OP_LSP | OP_LRP) begin
                     top <= alu_result;
                     cf <= alu_cout;
                  end
                  if ((OP_JNZ & ~top_zero) | OP_JSR) begin
                     irqf <= sf[1] ? 1'b0 : irqf;
                     {pc, slot} <= top[(WIDTHIA + WIDTHO - 1):0];
                     fsm <= FETCH;
                  end
                  else begin
                     slot <= next_slot;
                     pc <= next_pc;
                     if (last_slot) begin
                        fsm <= FETCH;
                     end
                  end
               end
               else begin
                  rf <= read_opcode;
                  d_write <= write_opcode;
               end
               d_read <= ~rf ? read_opcode : (d_read ? d_waitrequest : 1'b0);
            end
         endcase
      end
   end
endmodule
