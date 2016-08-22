module nano_top (
   input    logic             clock,
   input    logic             sreset,
   output   logic             led);
   
            logic [11:0]      i_address, d_address;
            logic [31:0]      i_readdata, d_readdata, d_writedata;
            logic             i_read, i_waitrequest, d_read, d_write, d_waitrequest, irq;
   nano5bit                   # (
                                 .WIDTHIA(12),
                                 .WIDTHID(32),
                                 .WIDTHDA(12),
                                 .WIDTHDD(32),
                                 .STKBASE('h1e0)
                              )
                              core (
                                 .clock(clock),
                                 .sreset(sreset),
                                 .i_address(i_address),
                                 .i_readdata(i_readdata),
                                 .i_read(i_read),
                                 .i_waitrequest(i_waitrequest),
                                 .d_address(d_address),
                                 .d_writedata(d_writedata),
                                 .d_readdata(d_readdata),
                                 .d_read(d_read),
                                 .d_write(d_write),
                                 .d_waitrequest(d_waitrequest),
                                 .irq(irq)
                              );
                              
            logic [31:0]      timer_readdata;
            logic             timer_read, timer_write, timer_waitrequest;
   timer                      timer (
                                 .clock(clock),
                                 .sreset(sreset),
                                 .address(d_address[2:0]),
                                 .readdata(timer_readdata),
                                 .writedata(d_writedata),
                                 .read(timer_read),
                                 .write(timer_write),
                                 .waitrequest(timer_waitrequest),
                                 .irq(irq)
                              );
            
            logic [31:0]      ram[0:511], ram_readdata;
            logic [31:0]      code[0:4095], code_readdata;
            logic             code_latency, ram_latency;
   
   initial begin
      $readmemh("code.hex", code);
   end
   
   always_comb begin
      i_readdata = code_readdata;
      i_waitrequest = i_read ? ~code_latency : 1'b0;
      d_waitrequest = 1'b0;
      d_readdata = ram_readdata;
      timer_read = 1'b0;
      timer_write = 1'b0;
      if (d_address < 12'h200) begin
         if (d_read) begin
            d_waitrequest = ~ram_latency;
         end
      end
      else begin
         if (d_address == 12'h800) begin
            d_readdata = led;
         end
         if (d_address[11:4] == 8'h81) begin
            d_readdata[15:0] = timer_readdata[15:0];
            timer_read = d_read;
            timer_write = d_write;
            d_waitrequest = timer_waitrequest;
         end
      end
   end
   always_ff @ (posedge clock) begin
      if (sreset) begin
         led <= 1'b0;
         code_latency <= 1'b0;
         ram_latency <= 1'b0;
         code_readdata <= {16{1'bx}};
         ram_readdata <= {16{1'bx}};
      end
      else begin
         code_readdata <= code[i_address];
         code_latency <= code_latency ? 1'b0 : i_read;
         ram_latency <= ram_latency ? 1'b0 : d_read;
         ram_readdata <= ram[d_address[7:0]];
         if (d_address < 12'h100) begin
            if (d_write) begin
               ram[d_address[8:0]] <= d_writedata;
            end
         end
         if ((d_address == 12'h800) & d_write) begin
            led <= d_writedata[0];
         end
      end
   end
endmodule
