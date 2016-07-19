`timescale 1ns/1ns
module nano_top_tb();

   logic          clock, sreset;
   
   initial begin
      clock = 1'b0;
      sreset = 1'b0;
      #40 sreset = 1'b1;
      #20 sreset = 1'b0;
   end
   
   always #20 clock = ~clock;
   
   logic             led;
   nano_top          dut(
                        .clock(clock),
                        .led(led)
                     );

endmodule
