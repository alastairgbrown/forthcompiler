module bridge32 # (
            parameter            WIDTHA = 8,
            parameter            WIDTHD = 16
)
(
   input    logic                clock,
   input    logic                sreset,
   
   input    logic [WIDTHA-1:0]   s_address,
   input    logic [WIDTHD/8-1:0] s_byteenable,
   input    logic [WIDTHD-1:0]   s_writedata,
   output   logic [WIDTHD-1:0]   s_readdata,
   input    logic                s_read,
   input    logic                s_write,
   output   logic                s_waitrequest,
   
   output   logic [WIDTHA-2:0]   d_address,
   output   logic [WIDTHD/4-1:0] d_byteenable,
   output   logic [WIDTHD*2-1:0] d_writedata,
   input    logic [WIDTHD*2-1:0] d_readdata,
   output   logic                d_read,
   output   logic                d_write,
   input    logic                d_waitrequest
);
            localparam           ZERO = {WIDTHA+WIDTHD{1'b0}}; // same or longer than either
            localparam           ONES = ~ZERO;
            localparam           ONE = {ZERO, 1'b1};
            logic [WIDTHD-1:0]   d_reg;
   
   // for now byeenables not handled
   assign d_byteenable = ONES[WIDTHD/4-1:0];
   
   always_comb begin
      s_waitrequest = s_address[0] & ((s_read | s_write) & ~d_waitrequest);
      s_readdata = s_address[0] ? d_readdata[WIDTHD*2-1:WIDTHD] : d_reg[WIDTHD-1:0];
      d_address = s_address[WIDTHA-1:1];
      d_read = s_read & s_address[0];
      d_write = s_write & s_address[0];
   end
   always_ff @ (posedge clock) begin
      d_reg <= d_read & ~d_waitrequest ? d_readdata[WIDTHD-1:0] : d_reg;
      d_writedata[WIDTHD-1:0] <= ~s_address[0] ? s_writedata : d_writedata[WIDTHD-1:0];
      d_writedata[WIDTHD*2-1:WIDTHD] <= s_address[0] ? s_writedata : d_writedata[WIDTHD*2-1:WIDTHD];
   end

endmodule

   