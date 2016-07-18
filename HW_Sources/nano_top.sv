module nano_top
(
   input    logic                clock,
   input    logic                sreset,
   output   logic                led
);

            localparam           SYS_CLOCK = 50000000;
            localparam           BASE_ADDR = 0;
   ///////////////////////////////////////////////////////////////////////////
   // Memory space
   ///////////////////////////////////////////////////////////////////////////
            localparam           CODE_ENABLE = 1;  // != 0 to include peripheral
            localparam           CODE_BASE = BASE_ADDR;
            localparam           CODE_SIZE = 1024;
            localparam           CODE_ADDRBITS = $clog2(CODE_SIZE);
            localparam           CODE_END_ADDR = CODE_BASE + ((1 << CODE_ADDRBITS) - 1);
            
   ///////////////////////////////////////////////////////////////////////////
            localparam           RAM_ENABLE = 1;
            localparam           RAM_BASE = CODE_END_ADDR + 1;
            localparam           RAM_SIZE = 256;
            localparam           RAM_ADDRBITS = $clog2(RAM_SIZE);
            localparam           RAM_END_ADDR = RAM_BASE + ((1 << RAM_ADDRBITS) - 1);
            
   ///////////////////////////////////////////////////////////////////////////
            localparam           IRQ_BASE = RAM_END_ADDR + 1;
            localparam           IRQ_SIZE = 8;
            localparam           IRQ_ADDRBITS = $clog2(IRQ_SIZE);
            localparam           IRQ_END_ADDR = IRQ_BASE + ((1 << IRQ_ADDRBITS) - 1);
            
   ///////////////////////////////////////////////////////////////////////////
            localparam           LED_ENABLE = 1;
            localparam           LED_BASE = IRQ_END_ADDR + 1;
            localparam           LED_SIZE = 8;
            localparam           LED_ADDRBITS = $clog2(LED_SIZE);
            localparam           LED_END_ADDR = LED_BASE + ((1 << LED_ADDRBITS) - 1);
            
   ///////////////////////////////////////////////////////////////////////////
            localparam           TIMER1_ENABLE = 0;
            localparam           TIMER1_BASE = LED_END_ADDR + 1;
            localparam           TIMER1_SIZE = 8;
            localparam           TIMER1_ADDRBITS = $clog2(TIMER1_SIZE);
            localparam           TIMER1_END_ADDR = TIMER1_BASE + ((1 << TIMER1_ADDRBITS) - 1);
            
   ///////////////////////////////////////////////////////////////////////////
            localparam           UART1_ENABLE = 0;
            localparam           UART1_BASE = TIMER1_END_ADDR + 1;
            localparam           UART1_SIZE = 4;
            localparam           UART1_ADDRBITS = $clog2(UART1_SIZE);
            localparam           UART1_END_ADDR = UART1_BASE + ((1 << UART1_ADDRBITS) - 1);
            
   ///////////////////////////////////////////////////////////////////////////
            localparam           WIDTHA = $clog2(UART1_END_ADDR); // the last pripheral in memory
            
   initial begin
      $display("===========================================");
      $display("Instruction memory space");
      if (CODE_ENABLE != 0) $display("CODE_BASE=0x%h", CODE_BASE);
      $display("===========================================");
      $display("Data memory space");
      if (RAM_ENABLE != 0) $display("RAM_BASE=0x%h", RAM_BASE);
      $display("IRQ_BASE=0x%h", IRQ_BASE);
      if (LED_ENABLE != 0) $display("LED_BASE=0x%h", LED_BASE);
      if (TIMER1_ENABLE != 0) $display("TIMER1_BASE=0x%h", TIMER1_BASE);
      if (UART1_ENABLE != 0) $display("UART_BASE=0x%h", UART1_BASE);
      $display("===========================================");
   end 
 

 
   ///////////////////////////////////////////////////////////////////////////
            logic [WIDTHA-1:0]   core_address;
            logic [31:0]         core_writedata, core_readdata;
            logic                core_read, core_write, core_waitrequest;
            wire                 cs_code = (core_address >= (CODE_BASE)) && (core_address < (CODE_BASE + CODE_SIZE)) && (CODE_ENABLE == 1);
            wire                 cs_ram = (core_address >= (RAM_BASE)) && (core_address < (RAM_BASE + RAM_SIZE)) && (RAM_ENABLE == 1);
            wire                 cs_irq = (core_address >= (IRQ_BASE)) && (core_address < (IRQ_BASE + IRQ_SIZE));
            wire                 cs_led = (core_address >= (LED_BASE)) && (core_address < (LED_BASE + LED_SIZE)) && (LED_ENABLE == 1);
            wire                 cs_timer1 = (core_address >= (TIMER1_BASE)) && (core_address < (TIMER1_BASE + TIMER1_SIZE)) && (TIMER1_ENABLE == 1);
            wire                 cs_uart1 = (core_address >= (UART1_BASE)) && (core_address < (UART1_BASE + UART1_SIZE)) && (UART1_ENABLE == 1);
   nano2                         # (
                                    .WIDTHA(WIDTHA),
                                    .WIDTHD(32)
                                 )
                                 core (
                                    .clock(clock),
                                    .sreset(sreset),
                                    .address(core_address),
                                    .writedata(core_writedata),
                                    .readdata(core_readdata),
                                    .read(core_read),
                                    .write(core_write),
                                    .waitrequest(core_waitrequest)
                                 );
                                 
   ///////////////////////////////////////////////////////////////////////////
            logic [31:0]         code_readdata;
            logic                code_waitrequest;
   generate
      if (CODE_ENABLE != 0) begin : code
   ram                           # (
                                    .WIDTHA(CODE_ADDRBITS),
                                    .WIDTHD(32),
                                    .FILE("code.hex")
                                 )
                                 mem_code (
                                    .clock(clock),
                                    .sreset(sreset),
                                    .address(core_address[CODE_ADDRBITS-1:0]),
                                    .writedata({32{1'bx}}),
                                    .readdata(code_readdata),
                                    .read(cs_code & core_read),
                                    .write(1'b0),
                                    .waitrequest(code_waitrequest)
                                 );
      end
   endgenerate
                                 
   ///////////////////////////////////////////////////////////////////////////
            logic [31:0]         ram_readdata;
            logic                ram_waitrequest;
   generate
      if (RAM_ENABLE != 0) begin : ram
   ram                           # (
                                    .WIDTHA(RAM_ADDRBITS),
                                    .WIDTHD(32),
                                    .FILE("")
                                 )
                                 mem_ram (
                                    .clock(clock),
                                    .sreset(sreset),
                                    .address(core_address[RAM_ADDRBITS-1:0]),
                                    .writedata(core_writedata),
                                    .readdata(ram_readdata),
                                    .read(cs_ram & core_read),
                                    .write(cs_ram & core_write),
                                    .waitrequest(ram_waitrequest)
                                 );
      end
   endgenerate

   ///////////////////////////////////////////////////////////////////////////
            logic [31:0]         led_readdata;
            logic                led_waitrequest;
   generate
      if (LED_ENABLE != 0) begin : pio
   pio                           # (
                                    .TYPE(0),
                                    .WIDTH(1)
                                 )
                                 pio_led (
                                    .clock(clock),
                                    .sreset(sreset),
                                    .address(core_address[LED_ADDRBITS-1:0]),
                                    .writedata(core_writedata),
                                    .readdata(led_readdata),
                                    .read(cs_led & core_read),
                                    .write(cs_led & core_write),
                                    .waitrequest(led_waitrequest),
                                    .irq(),
                                    .q(led)
                                 );
      end
   endgenerate

   ///////////////////////////////////////////////////////////////////////////
            logic [31:0]         timer1_readdata;
            logic                timer1_waitrequest, timer1_irq;
   generate
      if (TIMER1_ENABLE != 0) begin : timer1
         timer                         # (
                                    .PRESCALE(0)
                                 )
                                 timer1 (
                                    .clock(clock),
                                    .sreset(sreset),
                                    .address(core_address[TIMER1_ADDRBITS-1:0]),
                                    .writedata(core_writedata),
                                    .readdata(timer1_readdata),
                                    .read(cs_timer1 & core_read),
                                    .write(cs_timer1 & core_write),
                                    .waitrequest(timer1_waitrequest),
                                    .irq(timer1_irq)
                                 );
      end
   endgenerate

   ///////////////////////////////////////////////////////////////////////////
            logic [31:0]         uart1_readdata;
            logic                uart1_waitrequest, uart1_irq;
   generate
      if (UART1_ENABLE != 0) begin
   uart                          # (
                                    .SYSCLOCK(SYS_CLOCK),
                                    .BAUDRATE(115200),
                                    .SAMPLEBAUD(0)
                                 )
                                 uart1 (
                                    .clock(clock),
                                    .reset(sreset),
                                    .txd(uart1_txd),
                                    .rxd(uart1_rxd),
                                    .txd_ena(),
                                    .baudena_x16(),
                                    .address(core_address[UART1_ADDRBITS-1:0]),
                                    .writedata(core_writedata),
                                    .readdata(uart1_readdata),
                                    .read(cs_uart1 & core_read),
                                    .write(cs_uart1 & core_write),
                                    .waitrequest(uart1_waitrequest),
                                    .irq(uart1_irq)
                                 );
      end
   endgenerate

   ///////////////////////////////////////////////////////////////////////////
   always_comb begin
      core_readdata = code_readdata;
      core_waitrequest = 1'b0;
      core_readdata = {32{1'bx}};
      if (cs_code) begin
         core_readdata = code_readdata;
         core_waitrequest = code_waitrequest;
      end
      if (cs_ram) begin
         core_readdata = ram_readdata;
         core_waitrequest = ram_waitrequest;
      end
      if (cs_led) begin
         core_readdata = led_readdata;
         core_waitrequest = led_waitrequest;
      end
   end
   
endmodule

