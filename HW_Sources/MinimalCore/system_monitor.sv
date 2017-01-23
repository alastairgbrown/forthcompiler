/*
control system resets, uses external clock to react to failure of the PLL to gain lock or if it loses lock

gives graceful startup of pll and system logic.

*/
module system_monitor
(
   input    logic             ext_clock,
   input    logic             system_clock,
   input    logic             pll_locked,
   output   logic             pll_areset,    // asynch assert asynchronous de-assert
   output   logic             system_sreset  // synchronous to system_clock
);

enum        logic [7:0]       {RESET, PLL_RESET_ASSERT, PLL_RESET_DEASSERT, PLL_LOCK, RUNNING} fsm = RESET;

            logic [15:0]      count = 16'h0;
            logic [3:0]       meta_sample_locked;
            logic             system_areset = 1'b0;
            logic [3:0]       sreset_count = 4'h0;
            logic [1:0]       system_reset = 2'h0;

            
always_ff @ (posedge ext_clock) begin
   meta_sample_locked <= {meta_sample_locked[2:0], pll_locked};
end

always_ff @ (posedge ext_clock) begin
   case (fsm)
      RESET : begin
         system_areset <= 1'b1;
         pll_areset <= 1'b0;
         fsm <= PLL_RESET_ASSERT;
      end
      PLL_RESET_ASSERT : begin
         count <= count + 16'h1;
         pll_areset <= 1'b1;
         if (&count[3:0]) begin
            fsm <= PLL_RESET_DEASSERT;
         end
      end
      PLL_RESET_DEASSERT : begin
         count <= 16'h0;
         pll_areset <= 1'b0;
         fsm <= PLL_LOCK;
      end
      PLL_LOCK : begin
         count <= count + 16'h1;
         if (&count) begin
            // we have timed out waiting for pll_lock - something is wrong
            fsm <= RESET;
         end
         else begin
            if (meta_sample_locked[3]) begin
               fsm <= RUNNING;
            end
         end
      end
      RUNNING : begin
         system_areset <= 1'b0;
         if (~pll_locked) begin  // we have lost lock!
            fsm <= RESET;
         end
      end
   endcase
end

///////////////////////////////////////////////////////////////////

always_ff @ (posedge system_clock, posedge system_areset) begin   // asynch reset synch de-assert
   if (system_areset) begin
      system_reset <= 2'b00;
   end
   else begin
      system_reset <= {system_reset[0], 1'b1};
   end
end

always_ff @ (posedge system_clock) begin
   if (~system_reset[1]) begin
      sreset_count <= 4'h0;
      system_sreset <= 1'b1;
   end
   else begin
      if (&sreset_count) begin
         system_sreset <= 1'b0;
      end
      else begin
         system_sreset <= 1'b1;
         sreset_count <= sreset_count + 4'h1;
      end
   end
end

endmodule
