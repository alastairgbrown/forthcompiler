module nano2 #
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
   output   logic                read,
   output   logic                write,
   input    logic                waitrequest,
   input    logic                irq
);

            localparam           STKBASE = 'h3e0;
            localparam           PCRESET = 'h4;
            localparam           IRQADDR = 'h0;
            localparam           WIDTHS = 6;
            localparam           OPCODES = WIDTHD / 4;
            localparam           WIDTHI = OPCODES * 4;
   enum     logic [2:0]          {FETCH, EXECUTE, POPSTACK} fsm;
            logic [WIDTHD-1:0]   top, next;
            logic [WIDTHI-1:0]   ir;
            logic [WIDTHA-1:0]   pc;
            logic [WIDTHS-1:0]   sp;
            logic [1:0]          sf;
            logic                irqf, ff, cf, irqs;
            wire  [WIDTHS-1:0]   next_sp = sp + {{WIDTHS-1{1'b0}}, 1'b1};
            wire  [WIDTHA-1:0]   next_pc = pc + {{WIDTHA-1{1'b0}}, 1'b1};
            wire  [WIDTHI-1:0]   next_ir = {4'h0, ir[WIDTHI-1:4]};
            wire                 irq_event = irqs & ~irqf & ~ff & ~|sf;
            wire                 OP_NOP = (ir[3:0] == 4'h0);   // no operation
            wire                 OP_LDW = (ir[3:0] == 4'h1);   // load word
            wire                 OP_STW = (ir[3:0] == 4'h2);   // store word
            wire                 OP_PSH = (ir[3:0] == 4'h3);   // duplicate and push
            wire                 OP_POP = (ir[3:0] == 4'h4);   // pop
            wire                 OP_SWP = (ir[3:0] == 4'h5);   // swap
            wire                 OP_JNZ = (ir[3:0] == 4'h6);   // jump to top if next == 0
            wire                 OP_JSR = (ir[3:0] == 4'h7);   // swap pc and top
            wire                 OP_ADC = (ir[3:0] == 4'h8);   // 
            wire                 OP_ADD = (ir[3:0] == 4'h9);
            wire                 OP_SUB = (ir[3:0] == 4'ha);
            wire                 OP_AND = (ir[3:0] == 4'hb);
            wire                 OP_XOR = (ir[3:0] == 4'hc);
            wire                 OP_LSR = (ir[3:0] == 4'hd);
            wire                 OP_ZEQ = (ir[3:0] == 4'he);
            wire                 OP_LIT = (ir[3:0] == 4'hf);   // push value
            wire                 OP_ALU = OP_ADD|OP_ADC|OP_SUB|OP_AND|OP_XOR|OP_LSR|OP_ZEQ;
            wire                 last_opcode = ~|ir;
            wire                 top_zero = ~|top;
            wire                 immediate = (OP_LSR | OP_ZEQ | OP_NOP | OP_SWP | OP_JSR);
            wire                 stack_push = OP_LIT | OP_PSH | ((fsm == FETCH) & irq_event);
            wire                 stack_pop = OP_POP | OP_JNZ | OP_ADD | OP_ADC | OP_SUB | OP_AND | OP_XOR;
            wire  [WIDTHA-1:0]   stack = {STKBASE[WIDTHA-1:WIDTHS], sp + {{WIDTHS-1{stack_pop}}, (stack_pop|stack_push)}};
            wire  [WIDTHD+1:0]   adder = {1'b0, next, 1'b1} + {1'b0, top ^ {WIDTHD{OP_SUB}}, (OP_ADC & cf) | OP_SUB};
            wire  [WIDTHD-1:0]   alu_logic = OP_AND ? next & top : (OP_XOR ? next ^ top : (OP_LSR ? {1'b0, top[WIDTHD-1:1]} : {WIDTHD{top_zero}}));
            wire  [WIDTHD-1:0]   alu_result = OP_ADD|OP_ADC|OP_SUB ? adder[WIDTHD:1] : alu_logic;
            wire                 alu_cout = OP_ADD|OP_ADC|OP_SUB ? adder[WIDTHD+1] : (OP_LSR ? top[0] : cf);
            
   always_ff @ (posedge clock) begin
      if (sreset) begin
         read <= 1'b0;
         write <= 1'b0;
         ff <= 1'b0;
         writedata <= next;
         irqf <= 1'b0;
         sp <= {WIDTHS{1'b0}};
         sf <= 2'h0;
         pc <= PCRESET[WIDTHA-1:0];
         irqs <= 1'b0;
         top <= {WIDTHD{1'bx}};
         next <= {WIDTHD{1'bx}};
         ir <= {WIDTHI{1'bx}};
         cf <= 1'bx;
         fsm <= FETCH;
      end
      else begin
         irqs <= irqf ? 1'b0 : (irqs | irq);
         writedata <= next;
         case (fsm)
            FETCH : begin
               ir <= readdata;
               if (irq_event) begin
                  address <= stack;
                  if (write & ~waitrequest) begin
                     read <= 1'b0;
                     write <= 1'b0;
                     irqf <= 1'b1;
                     sp <= next_sp;
                     pc <= IRQADDR[WIDTHA-1:0];
                  end
                  else begin
                     write <= 1'b1;
                  end
               end
               else begin
                  address <= pc;
                  ff <= 1'b1;
                  if (read & ~waitrequest) begin
                     read <= 1'b0;
                     fsm <= EXECUTE;
                     pc <= next_pc;
                  end
                  else
                     read <= 1'b1;
               end
            end
            EXECUTE : begin
               ff <= 1'b0;
               address <= OP_LDW|OP_STW ? top[WIDTHA-1:0] : stack;
               if (immediate | ((read|write) & ~waitrequest)) begin
                  address <= OP_STW ? stack : pc;
                  read <= OP_STW|OP_LIT;
                  write <= 1'b0;
                  if (OP_LDW)
                     top <= readdata;
                  if (OP_JSR)
                     top <= pc;
                  if (OP_JSR | (OP_JNZ & ~top_zero))
                     pc <= top[WIDTHA-1:0];
                  if (OP_ALU)
                     top <= alu_result;
                  else
                     if (OP_SWP|stack_pop)
                        top <= next;
                  if (stack_push|OP_SWP) begin
                     next <= top;
                  end
                  if (OP_ALU)
                     cf <= alu_cout;
                  if (stack_pop)
                     next <= readdata;
                  sf <= OP_SWP ? {sf[0], 1'b1} : 2'b00;
                  sp <= stack[WIDTHS-1:0];
                  if (OP_LIT|OP_STW) begin
                     fsm <= POPSTACK;
                  end
                  else begin
                     ir <= next_ir;
                     if (last_opcode|OP_JSR|(OP_JNZ & ~top_zero)) begin
                        fsm <= FETCH;
                     end
                  end
               end
               else begin
                  read <= OP_LDW|stack_pop;
                  write <= OP_STW|stack_push;
               end
            end
            POPSTACK : begin
               address <= stack;
               if (read & ~waitrequest) begin
                  read <= 1'b0;
                  sp <= stack[WIDTHS-1:0];
                  if (OP_LIT) begin
                     pc <= next_pc;
                     top <= readdata;
                  end
                  else begin
                     top <= next;
                     next <= readdata;
                  end
                  ir <= next_ir;
                  if (last_opcode)
                     fsm <= FETCH;
                  else
                     fsm <= EXECUTE;
               end
               else begin
                  read <= 1'b1;
               end
            end
         endcase
      end
   end
   
endmodule
