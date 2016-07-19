
#if (pass == 0) {
   /* macros */
   #define zero { #if (DATAPATH > 16) { psh; psh; xor; } #if (DATAPATH <= 16) { psh 0; } }
   #define load n { #if (argc > 0) { psh n; } ldw; }
   #define store a, n { #if (argc > 1) { psh n; } #if (argc > 0) { psh a; } stw; pop; }
   #define pushc { zero; psh; adc; }
   #define popc { lsr; pop; }
   #define tor { load __sp; psh 1; add; lit __sp; st; swp; pop; store; }
   #define rto { load __sp; ld; load __sp; psh 1; sub; lit __sp; store; }
   /* rename some opcodes */

   /* math functions */
   #define incv v { load v; psh 1; add; lit v; store; }
   #define decv v { load v; psh 1; sub; lit v; store; }
   
   /* program flow control */
   #define jp n { psh n; jnz; flush_opcodes; }
   #define jpnz n { psh; zeq; swp; zeq; lit n; and; pop; jnz; }
   #define jpz n { psh; zeq; lit n; and; pop; jnz; }
   #define js n { psh n; cnz; }
   #define ret { jnz; flush_opcodes; }
   #define reti { swp; swp; ret; }
   #define jpeq n { xor; pop; jpz n; }
   #define jpneq n { xor; pop; jpnz n; }
   #define jplt n { sub; pushc; jpz n; }
   #define jpgte n { sub; pop2; pushc; jpnz n; }
   #define djnz n { psh 1; sub; pop; psh; jpnz n; pop; }
   #define jpnc n { zero2; adc; swp; zeq; lit n; and; pop; jnz; }
   
   #define shr n { psh; i=0; while (i<n) {i=i+1; swp; lsr; } pop; }
   #define shl n { i=0; while (i<n) { i=i+1; psh; add; pop; } }
}
