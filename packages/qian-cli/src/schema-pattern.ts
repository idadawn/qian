import {ordinalIgnoreCaseClasses} from './ordinal-case.js';

// packages/Common/SchemaPattern.cs 的逐条移植：JSON Schema 正则（pattern 与 patternProperties 的键）按 ECMA-262 可移植子集、
// 不带标志的语义逐码元解释，接受与拒绝、键名稳定性、OrdinalIgnoreCase 折叠与翻译结果都与 .NET 逐字一致。
// tck/profiles/key-vectors.json 由 .NET 生成，两端测试逐条比对；规则改动须两端同步，见 packages/Common/README.md。

/** 模式超出可移植子集或规模上限，对应 .NET 的 NotSupportedException。 */
export class UnsupportedPattern extends Error {}

/** 展开量词后字符、类、. 与锚点的总数上限，量词的每个边界也不能超过它；同 SchemaPattern.MaxSize。 */
export const maxSize=1000;
const cacheSize=128,universe=0x10000;
const cache=new Map<string,KeyPattern>();
// 方言判断不同的码元，同 SchemaPattern 的 Unstable。
const unstable=new Set([0x0A,0x0D,0xA0,0x130,0x1680,0x180E,...Array.from({length:11},(_,offset)=>0x2000+offset),0x2028,0x2029,0x202F,0x205F,0x3000,0xFEFF]);
const unsupported=():UnsupportedPattern=>new UnsupportedPattern('正则写法超出 ECMA-262 可移植子集或规模上限。');
const surrogate=(code:number):boolean=>code>=0xD800&&code<=0xDFFF;
const digit=(c:string|undefined):boolean=>c!==undefined&&c>='0'&&c<='9';

/** 键名不含上述码元与代理项时，接受的每种写法在各验证器方言与翻译结果下判断相同；折叠后同样成立。 */
export function isStable(name:string):boolean{
 for(let i=0;i<name.length;i++){const code=name.charCodeAt(i);if(unstable.has(code)||surrogate(code))return false;}
 return true;
}

/** 按模式与是否折叠缓存编译实例，满了整表清空；不支持的写法抛 UnsupportedPattern，不进缓存。 */
export function compilePattern(pattern:string,foldCase=false):KeyPattern{
 const key=(foldCase?'i':'s')+pattern,cached=cache.get(key);if(cached)return cached;
 const compiled=new KeyPattern(translatePattern(pattern,foldCase));
 if(cache.size>=cacheSize)cache.clear();
 cache.set(key,compiled);return compiled;
}

export const cachedPatterns=():number=>cache.size;

/**
 * 执行翻译结果的匹配器，用于键名与 Schema 验证器的字符串值。翻译结果只含码元转义、字符类、非捕获组、| ^ \z 与量词，
 * 这里按 Thompson 构造成自动机逐码元模拟：与 .NET NonBacktracking 一样线性时间、不回溯。不交给 JS 正则：折叠会让原本互斥的分支重叠，
 * 例如 ^(?:[a-z]|[A-Z])+$ 折叠后两个分支相同，回溯引擎遇到以 ! 结尾的长键名会指数级变慢；未折叠的模式同样可能写出这种分支。
 * ^ 只在开头、\z 只在末尾成立。toString 返回翻译结果：Ajv 按它在同一 Schema 内复用实例，翻译结果相同的模式匹配也相同。
 */
export class KeyPattern{
 // 每个状态：consume 为要消耗的码元集合（ε 状态为 undefined），condition 为 ε 状态的条件（0 无、1 开头、2 末尾），targets 为后继。
 private readonly consume:(Bitset|undefined)[]=[];
 private readonly condition:number[]=[];
 private readonly targets:number[][]=[];
 private readonly accept=this.add(undefined,0,[]);
 private readonly entry:number;
 constructor(readonly source:string){this.entry=this.build(new TranslatedReader(source).alternation(),this.accept);}
 toString():string{return this.source;}
 /** 键名中任一位置起有子串匹配即为真，同 Regex.IsMatch。 */
 test(text:string):boolean{
  const seen=new Int32Array(this.consume.length).fill(-1);
  // ε 闭包；同一位置访问过的状态不再访问，空循环也能终止。
  const close=(state:number,position:number,into:number[]):void=>{
   const stack=[state];
   while(stack.length){
    const current=stack.pop() as number;
    if(seen[current]===position)continue;
    seen[current]=position;
    if(this.consume[current])into.push(current);
    else if(this.condition[current]===0||(this.condition[current]===1?position===0:position===text.length))stack.push(...this.targets[current]);
   }
  };
  let active:number[]=[];
  for(let position=0;;position++){
   close(this.entry,position,active);
   if(seen[this.accept]===position)return true;
   if(position===text.length)return false;
   const code=text.charCodeAt(position),following:number[]=[];
   for(const state of active)if(this.consume[state]?.has(code))close(this.targets[state][0],position+1,following);
   active=following;
  }
 }
 private add(consume:Bitset|undefined,condition:number,targets:number[]):number{
  this.consume.push(consume);this.condition.push(condition);this.targets.push(targets);
  return this.consume.length-1;
 }
 // 从后往前构造：out 是匹配完该节点后的去处。重复先接可选份（每份都可直接去 out，无上界时是回到自身的环），再在前面接必需份。
 private build(node:Node,out:number):number{
  if('set' in node)return this.add(node.set,0,[out]);
  if('anchor' in node)return this.add(undefined,node.anchor,[out]);
  if('items' in node){for(let index=node.items.length-1;index>=0;index--)out=this.build(node.items[index],out);return out;}
  if('branches' in node)return this.add(undefined,0,node.branches.map(branch=>this.build(branch,out)));
  let entry=out;
  if(node.max===Infinity){entry=this.add(undefined,0,[]);this.targets[entry].push(this.build(node.node,entry),out);}
  else for(let copy=node.min;copy<node.max;copy++)entry=this.add(undefined,0,[this.build(node.node,entry),out]);
  for(let copy=0;copy<node.min;copy++)entry=this.build(node.node,entry);
  return entry;
 }
}

type Node={set:Bitset}|{anchor:1|2}|{items:Node[]}|{branches:Node[]}|{node:Node;min:number;max:number};

// 读回翻译结果。输入只来自 translatePattern：码元一律是四位十六进制转义，字符类只有码元与范围（空集写成 [^…]），分组都是 (?:，
// 锚点只有 ^ 与 \z，量词后可带惰性 ?（只影响匹配位置，不影响能否匹配）。
class TranslatedReader{
 private i=0;
 constructor(private readonly source:string){}
 alternation():Node{
  const branches=[this.sequence()];
  while(this.source[this.i]==='|'){this.i++;branches.push(this.sequence());}
  return branches.length===1?branches[0]:{branches};
 }
 private sequence():Node{
  const items:Node[]=[];
  for(let c=this.source[this.i];c!==undefined&&c!=='|'&&c!==')';c=this.source[this.i]){
   let node:Node;
   if(c==='^'){this.i++;node={anchor:1};}
   else if(c==='\\'&&this.source[this.i+1]==='z'){this.i+=2;node={anchor:2};}
   else if(c==='('){this.i+=3;node=this.alternation();this.i++;}
   else if(c==='['){
    this.i++;const negated=this.source[this.i]==='^';if(negated)this.i++;
    const set=new Bitset();
    while(this.source[this.i]!==']'){
     const low=this.code();let high=low;
     if(this.source[this.i]==='-'){this.i++;high=this.code();}
     set.addRange(low,high);
    }
    this.i++;node={set:negated?set.not():set};
   }
   else{const set=new Bitset();set.add(this.code());node={set};}
   items.push(this.quantified(node));
  }
  return {items};
 }
 private quantified(node:Node):Node{
  const c=this.source[this.i];let min=1,max=1;
  if(c==='*'){min=0;max=Infinity;}
  else if(c==='+')max=Infinity;
  else if(c==='?')min=0;
  else if(c==='{'){
   const end=this.source.indexOf('}',this.i),[low,high]=this.source.slice(this.i+1,end).split(',');
   min=Number(low);max=high===undefined?min:high===''?Infinity:Number(high);this.i=end;
  }
  else return node;
  this.i++;if(this.source[this.i]==='?')this.i++;
  return {node,min,max};
 }
 private code():number{const value=parseInt(this.source.slice(this.i+2,this.i+6),16);this.i+=6;return value;}
}


/** 每个单字符位置先算出可匹配的码元集合再输出；foldCase 时扩成 OrdinalIgnoreCase 等价类的并集。记号规则与输出同 SchemaPattern.Translate。 */
export function translatePattern(pattern:string,foldCase=false):string{
 const reader=new Reader(pattern),outer:number[]=[];
 // size 是当前分组到此为止的规模，outer 存各外层分组进入时的规模，last 是可加量词的上一单元（字符、类、. 或分组）的规模。
 let output='',quantifiable=false,size=0,last=0;
 const atom=(set:Bitset):void=>{output+=emit(set,foldCase);last=1;size=grow(size,1);quantifiable=true;};
 while(reader.i<pattern.length){
  const c=pattern[reader.i++];
  switch(c){
   case '\\':{const[value,shorthand]=reader.escape(false);atom(shorthand?shorthandSet(shorthand):single(value));break;}
   case '[':atom(reader.members());break;
   case '(':
    if(pattern[reader.i]==='?'){if(pattern[reader.i+1]!==':')throw unsupported();reader.i+=2;}
    output+='(?:';outer.push(size);size=0;quantifiable=false;break;
   case ')':{
    const enclosing=outer.pop();if(enclosing===undefined)throw unsupported();
    output+=')';last=size;size=grow(enclosing,size);quantifiable=true;break;
   }
   case '|':output+=c;quantifiable=false;break;
   case '^':output+=c;size=grow(size,1);quantifiable=false;break;
   // .NET 的 $ 还匹配末尾换行之前，ECMA-262 不带 m 标志时只在末尾成立。
   case '$':output+='\\z';size=grow(size,1);quantifiable=false;break;
   case '.':atom(dot());break;
   case '*':case '+':case '?':case '{':
    // 量词只能跟在字符、类、. 或分组之后；惰性 ? 之外不能叠加，未组成量词的 { 不当字面量。
    if(!quantifiable)throw unsupported();
    if(c==='{'){const[text,copies]=reader.repeat();output+=text;size=grow(size,last*(copies-1));}else output+=c;
    if(pattern[reader.i]==='?'){output+='?';reader.i++;}
    quantifiable=false;break;
   case '}':case ']':throw unsupported();
   default:atom(single(c.charCodeAt(0)));
  }
 }
 if(outer.length)throw unsupported();
 return output;
}

const grow=(size:number,amount:number):number=>{if(size+amount>maxSize)throw unsupported();return size+amount;};

class Reader{
 i=0;
 constructor(private readonly pattern:string){}
 // [] 与 [^] 在 .NET 与 ECMA-262 含义不同；类内未转义的 [ 可能构成 .NET 的类减法；未转义的 - 只能在类首或类尾。
 // 否定类里再含否定简写的写法拒绝；范围两端只能是单个字符，且不能倒置。
 members():Bitset{
  const pattern=this.pattern,negated=pattern[this.i]==='^';if(negated)this.i++;
  const set=new Bitset();
  for(let first=true;;first=false){
   if(this.i>=pattern.length||pattern[this.i]==='['||(first&&pattern[this.i]===']'))throw unsupported();
   if(pattern[this.i]===']'){this.i++;break;}
   if(pattern[this.i]==='-'){
    if(!first&&pattern[this.i+1]!==']')throw unsupported();
    set.add(0x2D);this.i++;continue;
   }
   const[low,shorthand]=this.member();
   if(shorthand){if(negated&&'DWS'.includes(shorthand))throw unsupported();set.or(shorthandSet(shorthand));continue;}
   let high=low;
   if(this.i+1<pattern.length&&pattern[this.i]==='-'&&pattern[this.i+1]!==']'){
    const next=pattern[++this.i];if(next==='-'||next==='[')throw unsupported();
    const[value,range]=this.member();if(range||value<low)throw unsupported();
    high=value;
   }
   if(surrogate(low)||surrogate(high))throw unsupported();
   set.addRange(low,high);
  }
  return negated?set.not():set;
 }
 private member():[number,string]{const c=this.pattern[this.i++];return c==='\\'?this.escape(true):[c.charCodeAt(0),''];}
 // 只认 \d \D \w \W \s \S、\t \n \r \f \v、\xHH、\uHHHH 与语法字符的转义（类内另可转义 -）；返回码元与简写字母。
 escape(inClass:boolean):[number,string]{
  if(this.i>=this.pattern.length)throw unsupported();
  const c=this.pattern[this.i++];
  switch(c){
   case 'd':case 'D':case 'w':case 'W':case 's':case 'S':return[0,c];
   case 't':return[0x09,''];
   case 'n':return[0x0A,''];
   case 'r':return[0x0D,''];
   case 'f':return[0x0C,''];
   case 'v':return[0x0B,''];
   case 'x':case 'u':{
    const digits=c==='x'?2:4,text=this.pattern.slice(this.i,this.i+digits);
    if(text.length<digits||!/^[0-9A-Fa-f]+$/.test(text))throw unsupported();
    this.i+=digits;return[parseInt(text,16),''];
   }
   default:
    if((c==='-'&&inClass)||'^$\\.*+?()[]{}|/'.includes(c))return[c.charCodeAt(0),''];
    throw unsupported();
  }
 }
 // {n}、{n,}、{n,m}，返回原文与计入规模的份数：{n} 为 n、{n,m} 为 m、{n,} 为 n+1，至少 1。n > m 拒绝。
 repeat():[string,number]{
  const pattern=this.pattern,start=this.i,low=this.bound();
  let copies=Math.max(low,1);
  if(pattern[this.i]===','){
   this.i++;
   if(digit(pattern[this.i])){const high=this.bound();if(high<low)throw unsupported();copies=Math.max(high,1);}
   else copies=low+1;
  }
  if(pattern[this.i]!=='}')throw unsupported();
  this.i++;
  return['{'+pattern.slice(start,this.i),copies];
 }
 private bound():number{
  const start=this.i;let value=0;
  while(digit(this.pattern[this.i]))if((value=value*10+this.pattern.charCodeAt(this.i++)-0x30)>maxSize)throw unsupported();
  if(this.i===start)throw unsupported();
  return value;
 }
}

// BMP 码元集合，按 32 位字存放。
class Bitset{
 readonly words=new Uint32Array(universe/32);
 has(code:number):boolean{return(this.words[code>>>5]>>>(code&31)&1)===1;}
 add(code:number):void{this.words[code>>>5]|=1<<(code&31);}
 addRange(low:number,high:number):void{for(let code=low;code<=high;code++)this.add(code);}
 or(other:Bitset):void{for(let i=0;i<this.words.length;i++)this.words[i]|=other.words[i];}
 not():Bitset{for(let i=0;i<this.words.length;i++)this.words[i]=~this.words[i];return this;}
 clone():Bitset{const copy=new Bitset();copy.words.set(this.words);return copy;}
 // 从 from 起第一个在（value 为 true）或不在集合里的码元，没有则为 universe。
 next(from:number,value:boolean):number{
  for(let code=from;code<universe;){
   const word=(value?this.words[code>>>5]:~this.words[code>>>5])>>>(code&31);
   if(word===0){code=(code|31)+1;continue;}
   return code+31-Math.clz32(word&-word);
  }
  return universe;
 }
}

function single(code:number):Bitset{
 if(surrogate(code))throw unsupported();
 const set=new Bitset();set.add(code);return set;
}

// \d \w 按 ECMA-262 只认 ASCII，\s 为 ECMA-262 的空白与行终止符，大写为补集。
function shorthandSet(letter:string):Bitset{
 const set=new Bitset(),lower=letter.toLowerCase();
 if(lower==='d')set.addRange(0x30,0x39);
 else if(lower==='w'){set.addRange(0x30,0x39);set.addRange(0x41,0x5A);set.addRange(0x61,0x7A);set.add(0x5F);}
 else for(const code of spaces)set.add(code);
 return letter===lower?set:set.not();
}

const spaces=[0x09,0x0A,0x0B,0x0C,0x0D,0x20,0xA0,0x1680,...Array.from({length:11},(_,offset)=>0x2000+offset),0x2028,0x2029,0x202F,0x205F,0x3000,0xFEFF];

// . 不匹配 ECMA-262 的四个行终止符；.NET 的 . 只排除 \n。
function dot():Bitset{
 const set=new Bitset();
 for(const code of [0x0A,0x0D,0x2028,0x2029])set.add(code);
 return set.not();
}

// 码元一律写成四位十六进制转义，翻译结果里不残留依赖方言的元字符；空集写成全集的补集。
function emit(input:Bitset,foldCase:boolean):string{
 const set=foldCase?fold(input):input,ranges:string[]=[];
 let count=0;
 for(let start=set.next(0,true);start<universe;){
  const end=set.next(start,false);
  ranges.push(end-1>start?hex(start)+'-'+hex(end-1):hex(start));
  count+=end-start;start=set.next(end,true);
 }
 if(count===1)return ranges[0];
 return count===0?'[^'+hex(0)+'-'+hex(0xFFFF)+']':'['+ranges.join('')+']';
}

const hex=(code:number):string=>'\\u'+code.toString(16).toUpperCase().padStart(4,'0');

// 集合扩成等价类的并集：码元 c 在结果里，当且仅当与 c 相等（OrdinalIgnoreCase）的某个码元在原集合里；未列出的码元自成一类。
function fold(set:Bitset):Bitset{
 const folded=set.clone();
 for(const members of caseTables().classes)if(members.some(code=>set.has(code)))for(const code of members)folded.add(code);
 return folded;
}

let tables:{classes:(readonly number[])[];bmp:Uint16Array;astral:Map<number,number>}|undefined;
function caseTables():NonNullable<typeof tables>{
 if(!tables){
  const classes:(readonly number[])[]=[],bmp=new Uint16Array(universe).map((_,code)=>code),astral=new Map<number,number>();
  for(const members of ordinalIgnoreCaseClasses){
   if(members[0]<universe){classes.push(members);for(const code of members)bmp[code]=members[0];}
   else for(const code of members)astral.set(code,members[0]);
  }
  tables={classes,bmp,astral};
 }
 return tables;
}

/**
 * 按 .NET 的 string.Equals(…, StringComparison.OrdinalIgnoreCase) 判断键名相等：IConfiguration 与 Web 默认反序列化都以它为准。
 * BMP 逐码元比较等价类；两侧同一位置都是完整代理对时按码点比较，残缺的代理项只与自身相等。
 * 不用 JS 的 toUpperCase / toLowerCase：它们按完整映射改变长度（ß 变 SS），也把 ſ、ı、开尔文符号等归到 ASCII 字母。
 */
export function sameKey(left:string,right:string):boolean{
 if(left.length!==right.length)return false;
 const {bmp,astral}=caseTables();
 for(let i=0;i<left.length;i++){
  const a=left.charCodeAt(i),b=right.charCodeAt(i);
  if(isHigh(a)&&isHigh(b)&&i+1<left.length){
   const x=left.charCodeAt(i+1),y=right.charCodeAt(i+1);
   if(!isLow(x)||!isLow(y)){if(a!==b)return false;continue;}
   const p=pair(a,x),q=pair(b,y);
   if(p!==q&&(astral.get(p)??p)!==(astral.get(q)??q))return false;
   i++;continue;
  }
  if(a!==b&&bmp[a]!==bmp[b])return false;
 }
 return true;
}

const isHigh=(code:number):boolean=>code>=0xD800&&code<=0xDBFF;
const isLow=(code:number):boolean=>code>=0xDC00&&code<=0xDFFF;
const pair=(lead:number,trail:number):number=>(lead-0xD800)*0x400+trail-0xDC00+0x10000;
