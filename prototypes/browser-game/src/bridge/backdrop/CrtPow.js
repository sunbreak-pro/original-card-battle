// Not a port of a C# file: System.Math.Pow as the C# oracle computes it.
//
// .NET's Math.Pow calls the C runtime's pow. On Linux x86-64 (where the parity oracle runs, glibc 2.39,
// a CPU with FMA) that is glibc's __pow_fma: sysdeps/ieee754/dbl-64/e_pow.c (ARM optimized-routines)
// built with -mfma -mavx2, so the explicit __builtin_fma calls are real FMAs and GCC contracts every
// single-use `a * b + c` of one basic block into an FMA as well. V8's Math.pow is a different
// algorithm and differs from it in the last bit about 1 time in 10 for the backdrop's inputs, which
// shows up in BackdropSpec.Luminance / Contrast (doubles the parity harness compares exactly).
//
// This is that algorithm, line for line, with the same tables (__pow_log_data and __exp_data, read out
// of the system libm.so.6) and an exact FMA emulation (Dekker product + Boldo–Melquiond round-to-odd
// sum). Checked bit for bit against glibc's pow on 320,000 inputs (the luminance's (c + 0.055) / 1.055
// ^ 2.4 for float c, x ^ 1.4 for float x in [0, 1], and wide random x, y), including the tiny-result
// special case. Used by BackdropSpec (Linear) and MiasmaLook (ShadeAt) — the only Math.Pow calls in
// the ported C#. Signaling NaNs are not told apart (JS cannot); everything else follows e_pow.c.
//
// The Unity build on another platform (MSVC CRT, Mono, IL2CPP) may round its own pow differently; the
// oracle is the reference here, and nothing the game draws or decides depends on the last bit.

// __pow_log_data.tab: per row invc, logc, logctail (the zero pad left out), as hex uint64.
const POW_LOG_TAB = [
  '3ff6a00000000000bfd62c82f2b9c8003cfab42428375680', '3ff6800000000000bfd5d1bdbf580800bd1ca508d8e0f720',
  '3ff6600000000000bfd5767717455800bd2362a4d5b6506d', '3ff6400000000000bfd51aad872df800bce684e49eb067d5',
  '3ff6200000000000bfd4be5f95777800bd041b6993293ee0', '3ff6000000000000bfd4618bc21c60003d13d82f484c84cc',
  '3ff5e00000000000bfd404308686a8003cdc42f3ed820b3a', '3ff5c00000000000bfd3a64c556948003d20b1c686519460',
  '3ff5a00000000000bfd347dd9a9880003d25594dd4c58092', '3ff5800000000000bfd2e8e2bae120003d267b1e99b72bd8',
  '3ff5600000000000bfd2895a13de88003d15ca14b6cfb03f', '3ff5600000000000bfd2895a13de88003d15ca14b6cfb03f',
  '3ff5400000000000bfd22941fbcf7800bd165a242853da76', '3ff5200000000000bfd1c898c1699800bd1fafbc68e75404',
  '3ff5000000000000bfd1675cababa8003d1f1fc63382a8f0', '3ff4e00000000000bfd1058bf9ae4800bd26a8c4fd055a66',
  '3ff4c00000000000bfd0a324e2739000bd0c6bee7ef4030e', '3ff4a00000000000bfd0402594b4d000bcf036b89ef42d7f',
  '3ff4a00000000000bfd0402594b4d000bcf036b89ef42d7f', '3ff4800000000000bfcfb9186d5e40003d0d572aab993c87',
  '3ff4600000000000bfcef0adcbdc60003d2b26b79c86af24', '3ff4400000000000bfce27076e2af000bd172f4f543fff10',
  '3ff4200000000000bfcd5c216b4fc0003d21ba91bbca681b', '3ff4000000000000bfcc8ff7c79aa0003d27794f689f8434',
  '3ff4000000000000bfcc8ff7c79aa0003d27794f689f8434', '3ff3e00000000000bfcbc286742d90003d194eb0318bb78f',
  '3ff3c00000000000bfcaf3c94e80c0003cba4e633fcd9066', '3ff3a00000000000bfca23bc1fe2b000bd258c64dc46c1ea',
  '3ff3a00000000000bfca23bc1fe2b000bd258c64dc46c1ea', '3ff3800000000000bfc9525a9cf45000bd2ad1d904c1d4e3',
  '3ff3600000000000bfc87fa06520d0003d2bbdbf7fdbfa09', '3ff3400000000000bfc7ab890210e0003d2bdb9072534a58',
  '3ff3400000000000bfc7ab890210e0003d2bdb9072534a58', '3ff3200000000000bfc6d60fe719d000bd10e46aa3b2e266',
  '3ff3000000000000bfc5ff3070a79000bd1e9e439f105039', '3ff3000000000000bfc5ff3070a79000bd1e9e439f105039',
  '3ff2e00000000000bfc526e5e3a1b000bd20de8b90075b8f', '3ff2c00000000000bfc44d2b6ccb80003d170cc16135783c',
  '3ff2c00000000000bfc44d2b6ccb80003d170cc16135783c', '3ff2a00000000000bfc371fc201e90003cf178864d27543a',
  '3ff2800000000000bfc29552f81ff000bd248d301771c408', '3ff2600000000000bfc1b72ad52f6000bd2e80a41811a396',
  '3ff2600000000000bfc1b72ad52f6000bd2e80a41811a396', '3ff2400000000000bfc0d77e7cd090003d0a699688e85bf4',
  '3ff2400000000000bfc0d77e7cd090003d0a699688e85bf4', '3ff2200000000000bfbfec9131dbe000bd2575545ca333f2',
  '3ff2000000000000bfbe27076e2b00003d2a342c2af0003c', '3ff2000000000000bfbe27076e2b00003d2a342c2af0003c',
  '3ff1e00000000000bfbc5e548f5bc000bd1d0c57585fbe06', '3ff1c00000000000bfba926d3a4ae0003d253935e85baac8',
  '3ff1c00000000000bfba926d3a4ae0003d253935e85baac8', '3ff1a00000000000bfb8c345d631a0003d137c294d2f5668',
  '3ff1a00000000000bfb8c345d631a0003d137c294d2f5668', '3ff1800000000000bfb6f0d28ae56000bd269737c93373da',
  '3ff1600000000000bfb51b073f0620003d1f025b61c65e57', '3ff1600000000000bfb51b073f0620003d1f025b61c65e57',
  '3ff1400000000000bfb341d7961be0003d2c5edaccf913df', '3ff1400000000000bfb341d7961be0003d2c5edaccf913df',
  '3ff1200000000000bfb16536eea380003d147c5e768fa309', '3ff1000000000000bfaf0a30c01180003d2d599e83368e91',
  '3ff1000000000000bfaf0a30c01180003d2d599e83368e91', '3ff0e00000000000bfab42dd711980003d1c827ae5d6704c',
  '3ff0e00000000000bfab42dd711980003d1c827ae5d6704c', '3ff0c00000000000bfa77458f632c000bd2cfc4634f2a1ee',
  '3ff0c00000000000bfa77458f632c000bd2cfc4634f2a1ee', '3ff0a00000000000bfa39e87b9fec0003cf502b7f526feaa',
  '3ff0a00000000000bfa39e87b9fec0003cf502b7f526feaa', '3ff0800000000000bf9f829b0e780000bd2980267c7e09e4',
  '3ff0800000000000bf9f829b0e780000bd2980267c7e09e4', '3ff0600000000000bf97b91b07d58000bd288d5493faa639',
  '3ff0400000000000bf8fc0a8b0fc0000bcdf1e7cf6d3a69c', '3ff0400000000000bf8fc0a8b0fc0000bcdf1e7cf6d3a69c',
  '3ff0200000000000bf7fe02a6b100000bd19e23f0dda40e4', '3ff0200000000000bf7fe02a6b100000bd19e23f0dda40e4',
  '3ff000000000000000000000000000000000000000000000', '3ff000000000000000000000000000000000000000000000',
  '3fefc000000000003f80101575890000bd10c76b999d2be8', '3fef8000000000003f90205658938000bd23dc5b06e2f7d2',
  '3fef4000000000003f98492528c90000bd2aa0ba325a0c34', '3fef0000000000003fa0415d89e740003d0111c05cf1d753',
  '3feec000000000003fa466aed42e0000bd2c167375bdfd28', '3fee8000000000003fa894aa149fc000bd197995d05a267d',
  '3fee4000000000003faccb73cdddc000bd1a68f247d82807', '3fee2000000000003faeea31c006c000bd0e113e4fc93b7b',
  '3fede000000000003fb1973bd1466000bd25325d560d9e9b', '3feda000000000003fb3bdf5a7d1e0003d2cc85ea5db4ed7',
  '3fed6000000000003fb5e95a4d97a000bd2c69063c5d1d1e', '3fed4000000000003fb700d30aeac0003cec1e8da99ded32',
  '3fed0000000000003fb9335e5d5940003d23115c3abd47da', '3fecc000000000003fbb6ac88dad6000bd1390802bf768e5',
  '3feca000000000003fbc885801bc40003d2646d1c65aacd3', '3fec6000000000003fbec739830a2000bd2dc068afe645e0',
  '3fec4000000000003fbfe89139dbe000bd2534d64fa10afd', '3fec0000000000003fc1178e8227e0003d21ef78ce2d07f2',
  '3febe000000000003fc1aa2b7e23f0003d2ca78e44389934', '3feba000000000003fc2d1610c8680003d039d6ccb81b4a1',
  '3feb8000000000003fc365fcb01590003cc62fa8234b7289', '3feb4000000000003fc4913d8333b0003d25837954fdb678',
  '3feb2000000000003fc527e5e4a1b0003d2633e8e5697dc7', '3feae000000000003fc6574ebe8c10003d19cf8b2c3c2e78',
  '3feac000000000003fc6f0128b757000bd25118de59c21e1', '3feaa000000000003fc7898d85445000bd1c661070914305',
  '3fea6000000000003fc8beafeb390000bd073d54aae92cd1', '3fea4000000000003fc95a5adcf700003d07f22858a0ff6f',
  '3fea0000000000003fca93ed3c8ae000bd28724350562169', '3fe9e000000000003fcb31d8575bd000bd0c358d4eace1aa',
  '3fe9c000000000003fcbd087383be000bd2d4bc4595412b6', '3fe9a000000000003fcc6ffbc6f01000bcf1ec72c5962bd2',
  '3fe96000000000003fcdb13db0d49000bd2aff2af715b035', '3fe94000000000003fce530effe710003cc212276041f430',
  '3fe92000000000003fcef5ade4dd0000bcca211565bb8e11', '3fe90000000000003fcf991c6cb3b0003d1bcbecca0cdf30',
  '3fe8c000000000003fd07138604d58003cf89cdb16ed4e91', '3fe8a000000000003fd0c42d676160003d27188b163ceae9',
  '3fe88000000000003fd1178e8227e800bd2c210e63a5f01c', '3fe86000000000003fd16b5ccbacf8003d2b9acdf7a51681',
  '3fe84000000000003fd1bf99635a68003d2ca6ed5147bdb7', '3fe82000000000003fd214456d0eb8003d0a87deba46baea',
  '3fe7e000000000003fd2bef07cdc90003d2a9cfa4a5004f4', '3fe7c000000000003fd314f1e1d36000bd28e27ad3213cb8',
  '3fe7a000000000003fd36b6776be10003d116ecdb0f177c8', '3fe78000000000003fd3c252773330003d183b54b606bd5c',
  '3fe76000000000003fd419b423d5e8003d08e436ec90e09d', '3fe74000000000003fd4718dc271c800bd2f27ce0967d675',
  '3fe72000000000003fd4c9e09e173000bd2e20891b0ad8a4', '3fe70000000000003fd522ae0738a0003d2ebe708164c759',
  '3fe6e000000000003fd57bf753c8d0003d1fadedee5d40ef', '3fe6c000000000003fd5d5bddf596000bd0a0b2a08a465dc',

// __exp_data.tab: per row tab[2k] (tail), tab[2k+1] (scale bits less k << 45), as hex uint64.
];
const EXP_TAB = [
  '00000000000000003ff0000000000000', '3c9b3b4f1a88bf6e3feff63da9fb3335', 'bc7160139cd8dc5d3fefec9a3e778061',
  'bc905e7a108766d13fefe315e86e7f85', '3c8cd2523567f6133fefd9b0d3158574', 'bc8bce8023f98efa3fefd06b29ddf6de',
  '3c60f74e61e6c8613fefc74518759bc8', '3c90a3e45b33d3993fefbe3ecac6f383', '3c979aa65d837b6d3fefb5586cf9890f',
  '3c8eb51a92fdeffc3fefac922b7247f7', '3c3ebe3d702f9cd13fefa3ec32d3d1a2', 'bc6a033489906e0b3fef9b66affed31b',
  'bc9556522a2fbd0e3fef9301d0125b51', 'bc5080ef8c4eea553fef8abdc06c31cc', 'bc91c923b9d5f4163fef829aaea92de0',
  '3c80d3e3e95c55af3fef7a98c8a58e51', 'bc801b15eaa593483fef72b83c7d517b', 'bc8f1ff055de323d3fef6af9388c8dea',
  '3c8b898c3f1353bf3fef635beb6fcb75', 'bc96d99c7611eb263fef5be084045cd4', '3c9aecf73e3a2f603fef54873168b9aa',
  'bc8fe782cb86389d3fef4d5022fcd91d', '3c8a6f4144a6c38d3fef463b88628cd6', '3c807a05b0e4047d3fef3f49917ddc96',
  '3c968efde3a8a8943fef387a6e756238', '3c875e18f274487d3fef31ce4fb2a63f', '3c80472b981fe7f23fef2b4565e27cdd',
  'bc96b87b3f71085e3fef24dfe1f56381', '3c82f7e16d09ab313fef1e9df51fdee1', 'bc3d219b1a6fbffa3fef187fd0dad990',
  '3c8b3782720c0ab43fef1285a6e4030b', '3c6e149289cecb8f3fef0cafa93e2f56', '3c834d754db0abb63fef06fe0a31b715',
  '3c864201e2ac744c3fef0170fc4cd831', '3c8fdd395dd3f84a3feefc08b26416ff', 'bc86a3803b8e5b043feef6c55f929ff1',
  'bc924aedcc4b50683feef1a7373aa9cb', 'bc9907f81b512d8e3feeecae6d05d866', 'bc71d1e83e9436d23feee7db34e59ff7',
  'bc991919b3ce1b153feee32dc313a8e5', '3c859f48a72a4c6d3feedea64c123422', 'bc9312607a28698a3feeda4504ac801c',
  'bc58a78f4817895b3feed60a21f72e2a', 'bc7c2c9b67499a1b3feed1f5d950a897', '3c4363ed60c2ac113feece086061892d',
  '3c9666093b0664ef3feeca41ed1d0057', '3c6ecce1daa103793feec6a2b5c13cd0', '3c93ff8e3f0f12303feec32af0d7d3de',
  '3c7690cebb7aafb03feebfdad5362a27', '3c931dbdeb54e0773feebcb299fddd0d', 'bc8f94340071a38e3feeb9b2769d2ca7',
  'bc87deccdc93a3493feeb6daa2cf6642', 'bc78dec6bd0f385f3feeb42b569d4f82', 'bc861246ec7b5cf63feeb1a4ca5d920f',
  '3c93350518fdd78e3feeaf4736b527da', '3c7b98b72f8a9b053feead12d497c7fd', '3c9063e1e21c54093feeab07dd485429',
  '3c34c7855019c6ea3feea9268a5946b7', '3c9432e62b64c0353feea76f15ad2148', 'bc8ce44a6199769f3feea5e1b976dc09',
  'bc8c33c53bef4da83feea47eb03a5585', 'bc845378892be9ae3feea34634ccc320', 'bc93cedd785658583feea23882552225',
  '3c5710aa807e19643feea155d44ca973', 'bc93b3efbf5e22283feea09e667f3bcd', 'bc6a12ad8734b9823feea012750bdabf',
  'bc6367efb86da9ee3fee9fb23c651a2f', 'bc80dc3d54e088513fee9f7df9519484', 'bc781f647e5a3ecf3fee9f75e8ec5f74',
  'bc86ee4ac08b7db03fee9f9a48a58174', 'bc8619321e55e68a3fee9feb564267c9', '3c909ccb5e09d4d33feea0694fde5d3f',
  'bc7b32dcb94da51d3feea11473eb0187', '3c94ecfd5467c06b3feea1ed0130c132', '3c65ebe1abd66c553feea2f336cf4e62',
  'bc88a1c52fb3cf423feea427543e1a12', 'bc9369b6f13b37343feea589994cce13', 'bc805e843a19ff1e3feea71a4623c7ad',
  'bc94d450d872576e3feea8d99b4492ed', '3c90ad675b0e8a003feeaac7d98a6699', '3c8db72fc1f0eab43feeace5422aa0db',
  'bc65b6609cc5e7ff3feeaf3216b5448c', '3c7bf68359f35f443feeb1ae99157736', 'bc93091fa71e3d833feeb45b0b91ffc6',
  'bc5da9b88b6c1e293feeb737b0cdc5e5', 'bc6c23f97c90b9593feeba44cbc8520f', 'bc92434322f4f9aa3feebd829fde4e50',
  'bc85ca6cd7668e4b3feec0f170ca07ba', '3c71affc2b91ce273feec49182a3f090', '3c6dd235e10a73bb3feec86319e32323',
  'bc87c504226222633feecc667b5de565', '3c8b1c86e3e231d53feed09bec4a2d33', 'bc91bbd1d3bcbb153feed503b23e255d',
  '3c90cc319cee31d23feed99e1330b358', '3c8469846e735ab33feede6b5579fdbf', 'bc82dfcd978e9db43feee36bbfd3f37a',
  '3c8c1a7792cb33873feee89f995ad3ad', 'bc907b8f4ad1d9fa3feeee07298db666', 'bc55c3d956dcaeba3feef3a2b84f15fb',
  'bc90a40e3da6f6403feef9728de5593a', 'bc68d6f438ad93343feeff76f2fb5e47', 'bc91eee26b588a353fef05b030a1064a',
  '3c74ffd70a5fddcd3fef0c1e904bc1d2', 'bc91bdfbfa9298ac3fef12c25bd71e09', '3c736eae30af0cb33fef199bdd85529c',
  '3c8ee3325c9ffd943fef20ab5fffd07a', '3c84e08fd10959ac3fef27f12e57d14b', '3c63cdaf384e1a673fef2f6d9406e7b5',
  '3c676b2c6c9219683fef3720dcef9069', 'bc808a1883ccb5d23fef3f0b555dc3fa', 'bc8fad5d3ffffa6f3fef472d4a07897c',
  'bc900dae3875a9493fef4f87080d89f2', '3c74a385a63d07a73fef5818dcfba487', 'bc82919e2040220f3fef60e316c98398',
  '3c8e5a50d5c192ac3fef69e603db3285', '3c843a59ac016b4b3fef7321f301b460', 'bc82d52107b43e1f3fef7c97337b9b5f',
  'bc892ab93b470dc93fef864614f5a129', '3c74b604603a88d33fef902ee78b3ff6', '3c83c5ec519d72713fef9a51fbc74c83',
  'bc8ff7128fd391f03fefa4afa2a490da', 'bc8dae98e223747d3fefaf482d8e67f1', '3c8ec3bc41aa20083fefba1bee615a27',
  '3c842b94c3a9eb323fefc52b376bba97', '3c8a64a931d185ee3fefd0765b6e4540', 'bc8e37bae43be3ed3fefdbfdad9cbe14',
  '3c77893b4d91cd9d3fefe7c1819e90d8', '3c5305c14160cc893feff3c22b8f71f1',
];

const dv = new DataView(new ArrayBuffer(8));
function asuint64(x) {
  dv.setFloat64(0, x);
  return dv.getBigUint64(0);
}
function asdouble(u) {
  dv.setBigUint64(0, BigInt.asUintN(64, u));
  return dv.getFloat64(0);
}
const hexd = (h) => asdouble(BigInt('0x' + h));
const U64 = (v) => BigInt.asUintN(64, v);

const N = 128; // 1 << POW_LOG_TABLE_BITS == 1 << EXP_TABLE_BITS
const T_invc = [];
const T_logc = [];
const T_logctail = [];
for (const r of POW_LOG_TAB) {
  T_invc.push(hexd(r.slice(0, 16)));
  T_logc.push(hexd(r.slice(16, 32)));
  T_logctail.push(hexd(r.slice(32, 48)));
}
const E_tab = [];
for (const r of EXP_TAB) E_tab.push(BigInt('0x' + r.slice(0, 16)), BigInt('0x' + r.slice(16, 32)));

// __pow_log_data
const Ln2hi = hexd('3fe62e42fefa3800');
const Ln2lo = hexd('3d2ef35793c76730');
const A = ['bfe0000000000000', 'bfe5555555555560', '3fe0000000000006', '3fe999999959554e',
  'bfe555555529a47a', 'bff2495b9b4845e9', '3ff0002b8b263fc3'].map(hexd);
// __exp_data
const InvLn2N = hexd('40671547652b82fe');
const Shift = hexd('4338000000000000');
const NegLn2hiN = hexd('bf762e42fefa0000');
const NegLn2loN = hexd('bd0cf79abc9e3b3a');
const C2 = hexd('3fdffffffffffdbd');
const C3 = hexd('3fc555555555543c');
const C4 = hexd('3fa55555cf172b91');
const C5 = hexd('3f81111167a4d017');

const OFF = 0x3fe6955500000000n;
const SIGN_BIAS = 0x800 << 7; // 0x800 << EXP_TABLE_BITS
const ONE_BITS = asuint64(1.0);
const INF_BITS = asuint64(Infinity);

// ---------------------------------------------------------------- exact fma

const SPLIT = 134217729; // 2^27 + 1 (Veltkamp)

/** The rounding error of s = a + b (Knuth's TwoSum). */
function twoSumError(a, b, s) {
  const bb = s - a;
  return (a - (s - bb)) + (b - bb);
}

/**
 * fma(a, b, c) rounded once, as the FMA instruction does: the exact product (Dekker), then
 * RN(th + RO(tl + ul)) (Boldo & Melquiond, "Emulation of FMA and correctly rounded sums: proved
 * algorithms using rounding to odd"). Exact for every operand pow feeds it (no overflow / underflow
 * in the product's halves).
 */
function fma(a, b, c) {
  const p = a * b;
  const ca = SPLIT * a;
  const ah = ca - (ca - a);
  const al = a - ah;
  const cb = SPLIT * b;
  const bh = cb - (cb - b);
  const bl = b - bh;
  const ul = ((ah * bh - p) + ah * bl + al * bh) + al * bl;
  const th = c + p;
  const tl = twoSumError(c, p, th);
  let v = tl + ul;
  const e = twoSumError(tl, ul, v);
  if (e !== 0) {
    // round to odd: an inexact sum with an even last bit moves one ulp towards the exact value
    const bits = asuint64(v);
    if ((bits & 1n) === 0n) v = asdouble((v > 0) === (e > 0) ? bits + 1n : bits - 1n);
  }
  return th + v;
}

// ---------------------------------------------------------------- e_pow.c

const top12 = (x) => Number(asuint64(x) >> 52n);

/** zeroinfnan(i): 2 * i - 1 >= 2 * asuint64(INFINITY) - 1, in uint64. */
function zeroinfnan(i) {
  return U64(2n * i - 1n) >= U64(2n * INF_BITS - 1n);
}

/** 0 if not an integer, 1 if odd, 2 if even (iy: a non-zero finite double's bits). */
function checkint(iy) {
  const e = Number((iy >> 52n) & 0x7ffn);
  if (e < 0x3ff) return 0;
  if (e > 0x3ff + 52) return 2;
  const s = BigInt(0x3ff + 52 - e);
  if (iy & ((1n << s) - 1n)) return 0;
  if (iy & (1n << s)) return 1;
  return 2;
}

const oflow = (sign) => (sign ? -Infinity : Infinity); // __math_oflow
const uflow = (sign) => (sign ? -0 : 0); // __math_uflow

/** log(x) as hi + tail, ix the bits of a positive normal x (or a normalised subnormal). */
function log_inline(ix) {
  const tmp = U64(ix - OFF);
  const i = Number((tmp >> 45n) % 128n);
  const k = Number(BigInt.asIntN(64, tmp) >> 52n); // arithmetic shift
  const iz = U64(ix - (tmp & (0xfffn << 52n)));
  const z = asdouble(iz);
  const kd = k;

  const invc = T_invc[i];
  const logc = T_logc[i];
  const logctail = T_logctail[i];

  const r = fma(z, invc, -1.0);

  const t1 = fma(kd, Ln2hi, logc);
  const t2 = t1 + r;
  const lo1 = fma(kd, Ln2lo, logctail);
  const lo2 = t1 - t2 + r;

  const ar = A[0] * r;
  const ar2 = r * ar;
  const ar3 = r * ar2;
  const hi = t2 + ar2;
  const lo3 = fma(ar, r, -ar2);
  const lo4 = t2 - hi + ar2;
  // p = ar3 * (A[1] + r * A[2] + ar2 * (A[3] + r * A[4] + ar2 * (A[5] + r * A[6]))), contracted
  const q = fma(ar2, fma(ar2, fma(r, A[6], A[5]), fma(r, A[4], A[3])), fma(r, A[2], A[1]));
  // lo = lo1 + lo2 + lo3 + lo4 + p, the last add contracted with p's multiply
  const lo = fma(ar3, q, lo1 + lo2 + lo3 + lo4);
  const y = hi + lo;
  return { hi: y, tail: hi - y + lo };
}

function specialcase(tmp, sbits, ki) {
  if ((ki & 0x80000000n) === 0n) {
    // k > 0, the exponent of scale might have overflowed by <= 460.
    sbits = U64(sbits - (1009n << 52n));
    const scale = asdouble(sbits);
    return 2 ** 1009 * fma(scale, tmp, scale);
  }
  // k < 0, need special care in the subnormal range.
  sbits = U64(sbits + (1022n << 52n));
  const scale = asdouble(sbits);
  // scale * tmp is one multiply with a use in the block below: GCC does not fuse it.
  const st = scale * tmp;
  let y = scale + st;
  if (Math.abs(y) < 1.0) {
    let one = 1.0;
    if (y < 0.0) one = -1.0;
    let lo = scale - y + st;
    const hi = one + y;
    lo = one - hi + y + lo;
    y = (hi + lo) - one;
    if (y === 0) y = asdouble(sbits & 0x8000000000000000n);
  }
  return 2 ** -1022 * y;
}

function exp_inline(x, xtail, sign_bias) {
  let abstop = top12(x) & 0x7ff;
  if (((abstop - top12(2 ** -54)) >>> 0) >= ((top12(512.0) - top12(2 ** -54)) >>> 0)) {
    if (((abstop - top12(2 ** -54)) >>> 0) >= 0x80000000) {
      // Avoid spurious underflow for tiny x.
      const one = 1.0 + x;
      return sign_bias ? -one : one;
    }
    if (abstop >= top12(1024.0)) {
      if (asuint64(x) >> 63n) return uflow(sign_bias);
      return oflow(sign_bias);
    }
    // Large x is special cased below.
    abstop = 0;
  }
  // exp(x) = 2^(k/N) * exp(r), with exp(r) in [2^(-1/2N),2^(1/2N)].
  let kd = fma(InvLn2N, x, Shift); // z = InvLn2N * x; kd = z + Shift, contracted
  const ki = asuint64(kd);
  kd -= Shift;
  let r = fma(kd, NegLn2loN, fma(kd, NegLn2hiN, x));
  r += xtail;
  const idx = Number(2n * (ki % BigInt(N)));
  const top = U64((ki + BigInt(sign_bias)) << 45n);
  const tail = asdouble(E_tab[idx]);
  const sbits = U64(E_tab[idx + 1] + top);
  const r2 = r * r;
  const tmp = fma(r2 * r2, fma(r, C5, C4), fma(r2, fma(r, C3, C2), tail + r));
  if (abstop === 0) return specialcase(tmp, sbits, ki);
  const scale = asdouble(sbits);
  return fma(scale, tmp, scale);
}

/** System.Math.Pow(x, y) as glibc's pow computes it (see the top of this file). */
export function Pow(x, y) {
  let sign_bias = 0;
  let ix = asuint64(x);
  const iy = asuint64(y);
  let topx = top12(x);
  const topy = top12(y);
  if (((topx - 0x001) >>> 0) >= 0x7ff - 0x001 || (((topy & 0x7ff) - 0x3be) >>> 0) >= 0x43e - 0x3be) {
    // Special cases: (x < 0x1p-126 or inf or nan) or (|y| < 0x1p-65 or |y| >= 0x1p63 or nan).
    if (zeroinfnan(iy)) {
      if (U64(2n * iy) === 0n) return 1.0;
      if (ix === ONE_BITS) return 1.0;
      if (U64(2n * ix) > U64(2n * INF_BITS) || U64(2n * iy) > U64(2n * INF_BITS)) return x + y;
      if (U64(2n * ix) === U64(2n * ONE_BITS)) return 1.0;
      if ((U64(2n * ix) < U64(2n * ONE_BITS)) === !(iy >> 63n)) return 0.0; // |x|<1 && y==inf or |x|>1 && y==-inf
      return y * y;
    }
    if (zeroinfnan(ix)) {
      let x2 = x * x;
      if (ix >> 63n && checkint(iy) === 1) x2 = -x2;
      return iy >> 63n ? 1 / x2 : x2;
    }
    // Here x and y are non-zero finite.
    if (ix >> 63n) {
      // Finite x < 0.
      const yint = checkint(iy);
      if (yint === 0) return NaN; // __math_invalid
      if (yint === 1) sign_bias = SIGN_BIAS;
      ix &= 0x7fffffffffffffffn;
      topx &= 0x7ff;
    }
    if ((((topy & 0x7ff) - 0x3be) >>> 0) >= 0x43e - 0x3be) {
      // Note: sign_bias == 0 here because y is not odd.
      if (ix === ONE_BITS) return 1.0;
      if ((topy & 0x7ff) < 0x3be) {
        // |y| < 2^-65, x^y ~= 1 + y*log(x).
        return ix > ONE_BITS ? 1.0 + y : 1.0 - y;
      }
      return (ix > ONE_BITS) === (topy < 0x800) ? oflow(0) : uflow(0);
    }
    if (topx === 0) {
      // Normalize subnormal x so exponent becomes negative.
      ix = asuint64(x * 2 ** 52);
      ix &= 0x7fffffffffffffffn;
      ix = U64(ix - (52n << 52n));
    }
  }

  const { hi, tail: lo } = log_inline(ix);
  const ehi = y * hi;
  const elo = fma(y, lo, fma(y, hi, -ehi));
  return exp_inline(ehi, elo, sign_bias);
}
