module dpram # (
            parameter         WIDTHA = 10,
            parameter         WIDTHD = 8,
            parameter         FILE = ""
)
(
   input    logic                clock,
   input    logic                sreset,
   input    logic [WIDTHA-1:0]   a_address,
   input    logic [WIDTHD-1:0]   a_writedata,
   output   logic [WIDTHD-1:0]   a_readdata,
   input    logic                a_read,
   input    logic                a_write,
   output   logic                a_waitrequest,
   output   logic                a_readdatavalid,
   input    logic [WIDTHA-1:0]   b_address,
   input    logic [WIDTHD-1:0]   b_writedata,
   output   logic [WIDTHD-1:0]   b_readdata,
   input    logic                b_read,
   input    logic                b_write,
   output   logic                b_waitrequest,
   output   logic                b_readdatavalid
);

   dc_dpram                      # (
                                    .DATA(WIDTHD),
                                    .ADDR(WIDTHA),
                                    .FILE(FILE)
                                 )
                                 ram (
                                    .a_clk(clock),
                                    .a_wr(a_write),
                                    .a_addr(a_address),
                                    .a_din(a_writedata),
                                    .a_dout(a_readdata),
                                    .b_clk(clock),
                                    .b_wr(b_write),
                                    .b_addr(b_address),
                                    .b_din(b_writedata),
                                    .b_dout(b_readdata)
                                 );
                                 
   always_comb begin
      a_waitrequest = 1'b0;
      b_waitrequest = 1'b0;
   end   
   always_ff @ (posedge clock) begin
      if (sreset) begin
         a_readdatavalid <= 1'b0;
         b_readdatavalid <= 1'b0;
      end
      else begin
         a_readdatavalid <= a_read;
         b_readdatavalid <= b_read;
      end
   end

endmodule
