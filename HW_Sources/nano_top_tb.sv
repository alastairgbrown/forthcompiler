`timescale 1ns/100ps
module nano_project_top_tb();

   logic          clock, sreset;
   
   initial begin
      clock = 1'b0;
      sreset = 1'b0;
      #40 sreset = 1'b1;
      #20 sreset = 1'b0;
      #600 sreset = 1'b1;
      #20 sreset = 1'b0;
   end
   
   always #20.35 clock = ~clock;
   
   logic             led;
   nano_project_top       dut(
                        .clock(clock),
                        .led(led)
                     );

endmodule
