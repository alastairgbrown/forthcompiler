/*
0x0 pio data reg
0x1 pio pin value
*/
module pio # (
            parameter         TYPE = 0,
            parameter         WIDTH = 1
)
(
   input    logic             clock,
   input    logic             sreset,
   input    logic [3:0]       address,
   input    logic [WIDTH-1:0]      writedata,
   output   logic [WIDTH-1:0]      readdata,
   input    logic             read,
   input    logic             write,
   output   logic             waitrequest,
   output   logic             readdatavalid,
   output   logic             irq,
   
   inout    wire  [WIDTH-1:0] q
);

            integer           i;
            logic [WIDTH-1:0] q_pin, q_ena, pio_reg, q_reg[1:0];

   assign   q = q_pin;
   always_comb begin
      irq = 1'b0;
      waitrequest = 1'b0;
      for (i=0; i<WIDTH; i=i+1) begin
         q_pin[i] = q_ena[i] ? pio_reg[i] : 1'bz;
      end
   end
   always_ff @ (posedge clock) begin
      if (sreset) begin
         q_ena <= {WIDTH{1'b0}};
         q_reg[0] <= {WIDTH{1'bx}};
         q_reg[1] <= {WIDTH{1'bx}};
         pio_reg <= {WIDTH{1'b0}};
         readdatavalid <= 1'b0;
      end
      else begin
         q_reg[0] <= q;
         q_reg[1] <= q_reg[0];
         if (TYPE == 0) begin
            q_ena <= {WIDTH{1'b1}};
            pio_reg <= (address == 4'h0) & write ? writedata[WIDTH-1:0] : pio_reg;
         end
         readdatavalid <= read;
         case (address)
            4'h0 : readdata[WIDTH-1:0] <= pio_reg;
            default : readdata[WIDTH-1:0] <= q_reg[1];
         endcase
      end
   end

endmodule
