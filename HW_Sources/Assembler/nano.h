   /* system constants */
   ARCH = 4;
   EXT = 0;
   OPMASK = (1 << ARCH) - 1;
   MSNIBBLE = (OPMASK << (ARCH *(ARCHNIBBLES-1)));
   MSBIT = (1 << (DATAPATH - 1));

	/***************************************************************************/
	/* define all opcodes and macro's to be used                               */
	/***************************************************************************/
	slot = 0;
   code = 0;
   sslot = 0;
   sdat = 0;
   vptr = 0;   /* start variables in RAM at memory 0x0 */

	/***************************************************************************/
	#if (pass == 0) {	/* only define once */
	
	   #define log n { l=0; t=n-1; while (t>0) { t=t>>1; l=l+1; } OFFSET = l; }
	   log (INSTRWIDTH / ARCH); /* bits to represent slot counter */
		#define flush_opcodes {
			#if (slot > 0) { out ptr, code; ptr = mem; out mem, 0; slot = 0; code = 0; } }
	  	#define add_opcode n { flag = 0; code = (code | ((n & OPMASK) << (ARCH * slot)));
	  		#if (slot >= ((INSTRWIDTH / ARCH) - 1)) { out ptr, code; ptr = mem; out mem, 0; code = 0; slot = 0; flag = 1; }
	  		#if (flag == 0) { slot = slot + 1; }
	  		flag = 0; }
	   #define add_char c { sflag = 0; sdat = sdat | ((c & 0xff) << (8 * sslot));
	      #if (sslot >= ((DATAPATH / 8)-1)) { out mem, sdat; sdat = 0; sslot = 0; sflag = 1; }
	      #if (sflag == 0) { sslot = sslot + 1; }
	      sflag = 0; }
	   #define flush_string {
	      add_char 0x0;  /* end of string marker */
	      #if (sslot != 0) { out mem, sdat; sslot = 0; sdat = 0; } }
	    
      /* Opcdes */
      #define lit  n { out mem, n; add_opcode 0xf; }
	   #define nop    { add_opcode 0x0; }
	   #define ldw    { add_opcode 0x1; }
	   #define stw    { add_opcode 0x2; }
      #define psh  n { #if (argc == 0) { add_opcode 0x3; } #if (argc > 0) { lit n; } }
	   #define pop	   { add_opcode 0x4; }
      #define swp    { add_opcode 0x5; }
      #define jnz  n { #if (argc > 0) { psh n; } add_opcode 0x6; }  
      #define jsr  n { #if (argc > 0) { psh n; } add_opcode 0x7; flush_opcodes; }
	   
	   /* ALU functions */
      #define adc  n { #if (argc > 0) { psh n; } add_opcode 0x8; }
      #define add  n { #if (argc > 0) { psh n; } add_opcode 0x9; }
   	#define sub  n { #if (argc > 0) { psh n; } add_opcode 0xa; }  
   	#define and  n { #if (argc > 0) { psh n; } add_opcode 0xb; }  
   	#define xor  n { #if (argc > 0) { psh n; } add_opcode 0xc; }  
   	#define lsr    { add_opcode 0xd; }  
   	#define zeq    { add_opcode 0xe; }  
   	
		#define variable n { n = vptr; vptr=vptr+1; }
		#define lbl x     {  flush_opcodes; x = ptr;    }
		#define strlbl x  { x = mem; }
		#define setstring n	{ n = mem; }
		#define alloc n, m  { n = vptr; vptr = vptr +  m; }
		#define data a, b { out var, (a<<8)|b; }	   
	}
