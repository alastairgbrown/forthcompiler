/*
0-7 PFX 0..7
         EXT
8 ldw    adc
9 stw    sub
a psh    and
b pop    xor
c swp    lsr
d jnz    zeq
e jsr    byt
f ext->    
*/
module nano5 #
(
            parameter            WIDTHA = 12,
            parameter            WIDTHD = 32
)
(
   input    logic                clock,
   input    logic                sreset,
   output   logic [WIDTHA-1:0]   address,
   output   logic [WIDTHD-1:0]   writedata,
   input    logic [WIDTHD-1:0]   readdata,
   output   logic [WIDTHD/8-1:0] byteenable,
   output   logic                read,
   output   logic                write,
   input    logic                waitrequest,
   input    logic                irq
);

            localparam           DSTKBASE = 'h4e0;
            localparam           PCRESET = 'h4;
            localparam           IRQADDR = 'h0;
            localparam           WIDTHS = 5;
            localparam           WIDTHR = 5;
            localparam           OPCODES = WIDTHD / 4;
            localparam           WIDTHO = $clog2(OPCODES);
            localparam           WIDTHI = OPCODES * 4;
            localparam           WIDTHK = WIDTHA+WIDTHD; // larger than both
            localparam           ZERO = {WIDTHK{1'b0}};
            localparam           ONES = ~ZERO;
            localparam           ONE = {{WIDTHK-1{1'b0}}, 1'b1};
            localparam           DONTCARE = {WIDTHK{1'bx}};
   enum     logic [2:0]          {FETCH, EXECUTE} fsm;
            logic [WIDTHD-1:0]   top, next;
            logic [OPCODES-1:0][3:0]   ir;
            logic [WIDTHA-1:0]   pc;
            logic [WIDTHS-1:0]   sp;
            logic [WIDTHO-1:0]   slot;
            logic [1:0]          sf;
            logic                irqf, ff, cf, irqs, pf, ef;
            wire                 last_slot = (slot >= (OPCODES - 1));
            wire  [WIDTHO-1:0]   next_slot = last_slot ? ZERO[WIDTHO-1:0] : slot + ONE[WIDTHO-1:0];
            wire  [WIDTHA-1:0]   next_pc = pc + last_slot;
            wire  [3:0]          instr = ir[slot];
            wire                 irq_event = irqs & ~irqf & ~ff & ~|sf;
            wire                 OP_PFX = ~instr[3];
            wire                 OP_LDW = (instr[3:0] == 4'h8);   // load word
            wire                 OP_STW = (instr[3:0] == 4'h9);   // store word
            wire                 OP_PSH = (instr[3:0] == 4'ha);   // duplicate and push
            wire                 OP_POP = (instr[3:0] == 4'hb);   // pop
            wire                 OP_SWP = (instr[3:0] == 4'hc);   // swap
            wire                 OP_JNZ = (instr[3:0] == 4'hd);   // jump to top if next == 0
            wire                 OP_JSR = (instr[3:0] == 4'he);   // swap pc and top
            wire                 OP_EXT = (instr[3:0] == 4'hf);   // 
            wire                 OP_ADC = (instr[3:0] == 4'h0);   // extended opcodes
            wire                 OP_SUB = (instr[3:0] == 4'h1);
            wire                 OP_AND = (instr[3:0] == 4'h2);
            wire                 OP_XOR = (instr[3:0] == 4'h3);
            wire                 OP_LSR = (instr[3:0] == 4'h4);
            wire                 OP_ZEQ = (instr[3:0] == 4'h5);
            wire                 OP_BYT = (instr[3:0] == 4'h6);
            wire                 OP_ALU = OP_ADC | OP_AND | OP_XOR | OP_LSR | OP_ZEQ;
            wire                 top_zero = ~|top;
            wire                 immediate = (OP_EXT | OP_ALU | OP_SWP | OP_JSR | OP_PFX);
            wire                 stack_push = OP_PSH;
            wire                 stack_pop = OP_POP | OP_JNZ;
            wire  [WIDTHD+1:0]   adder = {1'b0, next, 1'b1} + {1'b0, top^{WIDTHD{OP_SUB}}, cf|OP_SUB};
            wire  [WIDTHD-1:0]   alu_logic = OP_AND ? next & top : (OP_XOR ? next ^ top : (OP_LSR ? {1'b0, top[WIDTHD-1:1]} : {WIDTHD{top_zero}}));
            wire  [WIDTHD-1:0]   alu_result = OP_ADC ? adder[WIDTHD:1] : alu_logic;
            wire                 alu_cout = OP_ADC ? adder[WIDTHD+1] : (OP_LSR ? top[0] : cf);
            
   always_comb begin
      address = top[WIDTHA-1:0];
      byteenable = ONES[WIDTHD/8-1:0]; // for now
      writedata = next;
      case (fsm)
         FETCH : begin
            writedata = {pc, slot};
            write = irq_event;
            read = ~irq_event;
            if (irq_event)
               address = {DSTKBASE[WIDTHA-1:WIDTHS], sp + ONE[WIDTHS-1:0]};
            else
               address = pc;
         end
         EXECUTE : begin
            read = OP_LDW | stack_pop;
            write = OP_STW | stack_push;
            if (stack_push|stack_pop) begin
               address = {DSTKBASE[WIDTHA-1:WIDTHS], sp + {ZERO[WIDTHS-1:1], stack_push}};
            end
         end
      endcase
   end
   always_ff @ (posedge clock) begin
      if (sreset) begin
         ff <= 1'b0;
         irqf <= 1'b0;
         sp <= ZERO[WIDTHS-1:0];
         sf <= 2'h0;
         pf <= 1'b0;
         ef <= 1'b0;
         slot <= ZERO[WIDTHO-1:0];
         pc <= PCRESET[WIDTHA-1:0];
         irqs <= 1'b0;
         top <= DONTCARE[WIDTHD-1:0];
         next <= DONTCARE[WIDTHD-1:0];
         ir <= DONTCARE[WIDTHI-1:0];
         cf <= 1'b0;
         fsm <= FETCH;
      end
      else begin
         irqs <= irqf ? 1'b0 : (irqs | irq);
         case (fsm)
            FETCH : begin
               ir <= readdata;
               if (irq_event) begin
                  if (~waitrequest) begin
                     next <= top;
                     top <= {pc, slot};
                     irqf <= 1'b1;
                     sp <= sp + ONE[WIDTHS-1:0];
                     pc <= IRQADDR[WIDTHA-1:0];
                     slot <= ZERO[WIDTHO-1:0];
                  end
               end
               else begin
                  ff <= 1'b1;
                  if (~waitrequest) begin
                     fsm <= EXECUTE;
                  end
               end
            end
            EXECUTE : begin
               ff <= 1'b0;
               if (ef) begin
                  next <= alu_result;
                  cf <= alu_cout;
                  ef <= 1'b0;
                  slot <= next_slot;
                  pc <= next_pc;
                  if (last_slot)
                     fsm <= FETCH;
               end
               else begin
                  if (immediate | ~waitrequest) begin
                     ef <= OP_EXT;
                     pf <= OP_PFX;
                     if (OP_PFX | OP_LDW | OP_SWP | OP_JNZ | OP_JSR | stack_pop)
                        top <= OP_PFX ? (pf ? {top[WIDTHD-4:0], instr[2:0]} : instr[2:0]) : OP_LDW ? readdata :
                           OP_JSR ? {next_pc, next_slot} : next;
                     if (OP_SWP|stack_push|stack_pop)
                        next <= stack_pop ? readdata : top;
                     sf <= OP_SWP ? {sf[0], 1'b1} : 2'b00;
                     if (OP_JSR|(OP_JNZ & ~top_zero)) begin
                        pc <= top[WIDTHA+WIDTHO-1:WIDTHO];
                        slot <= top[WIDTHO-1:0];
                     end
                     sp <= sp + {{WIDTHS-1{stack_pop}}, stack_push | stack_pop};
                     irqf <= sf[1] & OP_JNZ ? 1'b0 : irqf;
                     if (OP_JSR | (OP_JNZ & ~top_zero)) begin
                        fsm <= FETCH;
                     end
                     else begin
                        slot <= next_slot;
                        pc <= next_pc;
                        if (last_slot)
                           fsm <= FETCH;
                     end
                  end
               end
            end
         endcase
      end
   end
   
endmodule
