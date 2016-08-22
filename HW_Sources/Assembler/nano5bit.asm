   /***************************************************************************/
   /* define physical constants                                               */
   /***************************************************************************/
   passes = 20;      /* number passes of all source code  */
   
	DATAPATH = 16;    /* number of bits for internal data path */
   INSTRWIDTH = 16;

   SYS_CLOCK = 100000000;

   IRQ_ADDRESS = 0x0; /* 0x0 ->0x3 for IRQ code, or vector to main IRQ service */
   CODE_START = 0x4;  /* Code starts executing here */
   VARS_START = 0x0;  /* position in RAM for variables */

	/***************************************************************************/
	/* opcode definition */
   slot = 0;
   code = 0;
   vptr = VARS_START;
   #if (pass == 0) {
      ARCH = 5; /* 5 bit opcodes */
      ARCHNIBBLES = DATAPATH / ARCH;
      OPMASK = (1 << ARCH) - 1;
      MSNIBBLE = (OPMASK << (ARCH *(ARCHNIBBLES-1)));
      MSBIT = (1 << (DATAPATH - 1));
      ONES = (1 << DATAPATH) - 1;
      #define log n { l=0; t=n-1; while (t>0) { t=t>>1; l=l+1; } OFFSET = l; }
      log (INSTRWIDTH / ARCH); /* bits to represent slot counter */
		#define variable n { n = vptr; vptr=vptr+1; }
		#define lbl x     {  x = (ptr << OFFSET)|slot; }
		#define alloc n, m  { n = vptr; vptr = vptr +  m; }
		#define data a, b { out var, (a<<8)|b; }	   
      #define flush_opcodes {
         #if (slot > 0) { out ptr, code; ptr = mem; out mem, 0; slot = 0; code = 0; }
      }
      #define add_opcode n {
         flag = 0;
         code = (code | ((n & OPMASK) << (ARCH * slot)));
         #if (slot >= ((INSTRWIDTH / ARCH) - 1)) {
            out ptr, code; ptr = mem; out mem, 0; code = 0; slot = 0; flag = 1;
         }
         #if (flag == 0) {
            slot = slot + 1;
         }
         flag = 0;
      }
      #define OP_PFX n  { add_opcode (n & 0xf); }
      #define OP_LDW    { add_opcode 0x10; }
      #define OP_STW    { add_opcode 0x11; }
      #define OP_PSH    { add_opcode 0x12; }
      #define OP_POP    { add_opcode 0x13; }
      #define OP_SWP    { add_opcode 0x14; }
      #define OP_JNZ    { add_opcode 0x15; }
      #define OP_JSR    { add_opcode 0x16; }
      #define OP_ADD    { add_opcode 0x17; }
      #define OP_ADC    { add_opcode 0x18; }
      #define OP_SUB    { add_opcode 0x19; }
      #define OP_AND    { add_opcode 0x1a; }
      #define OP_IOR    { add_opcode 0x1b; }
      #define OP_XOR    { add_opcode 0x1c; }
      #define OP_MLT    { add_opcode 0x1d; }
      #define OP_LSR    { add_opcode 0x1e; }
      #define OP_ZEQ    { add_opcode 0x1f; }
      
      #define lit n    {
   	   m = n & ONES;
         pfxbits = ARCH - 1;
   	   mbits = (1<<pfxbits)-1;
   	   mm = mbits<<((ARCHNIBBLES-1)*pfxbits);
   	   mf = 0;
   	   mc = ARCHNIBBLES-1;
   	   while (mm > 0) {
   	      md = (mm & m)>>(mc*pfxbits);
   	      mm = mm >> pfxbits;
   	      mc = mc - 1;
   	      #if (mf == 0) {
               #if (md != 0) {
                  mf = 1;
               }
            }
   	      #if (mf == 1) {
               OP_PFX md;
            }
   	   }
   	   #if (mf == 0) {
            OP_PFX 0x0;
         }
     	}
      #define dup { OP_PSH; }
      #define drop { OP_POP; }
      #define swap { OP_SWP; }
      #define lsr { OP_LSR; }
      #define push n { dup; #if (argc > 0) { lit n; } }
      #define add n { #if (argc > 0) { push n; } OP_ADD; }
      #define adc n { #if (argc > 0) { push n; } OP_ADC; }
      #define sub n { #if (argc > 0) { push n; } OP_SUB; }
      #define and n { #if (argc > 0) { push n; } OP_AND; }
      #define or n { #if (argc > 0) { push n; } OP_IOR; }
      #define xor n { #if (argc > 0) { push n; } OP_XOR; }
      #define jp n { #if (argc > 0) { push n; } OP_JNZ; }
      #define js n { #if (argc > 0) { push n; } OP_JSR; }
      #define jpz n { OP_ZEQ; and n; OP_JNZ; }
      #define jpnz n { OP_ZEQ; OP_ZEQ; push n; OP_AND; OP_JNZ; }
      #define load n { #if (argc > 0) { push n; } OP_LDW; }
      #define store a, n { #if (argc > 1) { push n; } #if (argc > 0) { push a; } OP_STW; OP_POP; OP_POP; }
      #define ret { jp; }
      #define reti { swap; swap; jp; }
      #define pushc { push 0; adc 0; }
      #define popc { lsr; drop; }
      #define djnz n { push 1; OP_SUB; OP_PSH; jpnz n; OP_POP; }
   }

	/***************************************************************************/
	/* actual peripheral base addresses */
   CODE_BASE = 0x000;
   RAM_BASE = 0x000;
   LED_BASE = 0x800;
   TIMER_BASE = 0x810;
	/***************************************************************************/
	
	/***************************************************************************/
   /* other constants */

	/***************************************************************************/
   /* Entry Point for CPU interrupt                                           */
	/***************************************************************************/

	/***************************************************************************/
   mem = IRQ_ADDRESS;
   ptr = mem;
   out mem, 0; /* set mem and ptr to correct relative positions */
   
   jp __ISR_SERVICE;
   flush_opcodes;
	/***************************************************************************/
	/***************************************************************************/
   /* Global Variables                                                        */
	/***************************************************************************/
   vptr = RAM_BASE;   /* start variables in RAM */
   variable xptr;
   variable sum;
   
	/***************************************************************************/
   /* Entry point for CPU reset                                               */
	/***************************************************************************/

	/***************************************************************************/
  	mem = CODE_START; /* starting point in memory for code */
  	ptr = mem;
  	out mem, 0;	/* set mem and ptr to correct relative positions */
	/***************************************************************************/

	/***************************************************************************/
   /* Main Code                                                               */
	/***************************************************************************/
	
	/***************************************************************************/
   lbl Main_Code;
   
      /* init variables */
      
      /* main thread of code */   

      store TIMER_BASE + 1, 0x10;
      store TIMER_BASE + 3, 0x10;
      store TIMER_BASE + 0, 0xf;
      
      
   lbl CheckSum;
      jp CheckSum;
      
	/***************************************************************************/
   lbl Main_Spin;	/* should never get here */
      store LED_BASE, 0;
      jp Main_Spin;      
	/***************************************************************************/
   	
	/***************************************************************************/
   /*                                                                         */
   /* Subroutines                                                             */
   /*                                                                         */
	/***************************************************************************/
   
	/***************************************************************************/
   lbl LED_Flop;
      load LED_BASE; push 1; OP_XOR; store LED_BASE;
      ret;
      
	/***************************************************************************/
   /* Interrupt Service Routines                                              */
	/***************************************************************************/

   /***************************************************************************/	
   flush_opcodes;
   lbl __ISR_SERVICE;      /* decide which IRQ has triggered */
      pushc;   /* preserve carry */
      store TIMER_BASE + 2, 1;
      js LED_Flop;
   
   lbl __ISR_EXIT;
      popc; /* restore carry */
      reti; /* return and clear irq state */
      
   /***************************************************************************/

   /***************************************************************************/
	/***************************************************************************/
   /* End Of Interrupt Service Routines                                       */
	/***************************************************************************/

	/***************************************************************************/
   
	/***************************************************************************/
   /* End of Code                                                             */
	/***************************************************************************/

	/***************************************************************************/
   /* Strings -                                                               */
	/***************************************************************************/

	/***************************************************************************/
   /* THE END OF SOURCE                                                       */
	/***************************************************************************/

	/***************************************************************************/
	
	/* dump the code at the end of passes */
	#if (pass == 19) { flush_opcodes; dump 0x0, mem; } 
	/***************************************************************************/
