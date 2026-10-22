import {spawn} from 'node:child_process';

// 此进程是专用进程组的首进程。实际包命令只能在父进程持久化归属后启动。
// 登记前父进程被杀：IPC 断开即退出，绝不启动可能写入文件的包命令。
let started=false,finished=false;
const keepAlive=setInterval(()=>{},60_000);
process.once('disconnect',()=>{if(!started||finished){clearInterval(keepAlive);process.exit(0);}});
process.once('message',(value:unknown)=>{
 const request=value as {type?:string;executable?:string;args?:string[]};
 if(started||request.type!=='run'||typeof request.executable!=='string'||!Array.isArray(request.args)||request.args.some(arg=>typeof arg!=='string'))process.exit(1);
 started=true;
 const child=spawn(request.executable!,request.args!,{shell:false,windowsHide:true,stdio:['ignore','inherit','inherit']});
 const finish=(code:number|null):void=>{if(finished)return;finished=true;
  if(process.connected)process.send?.({type:'result',code},()=>{});else{clearInterval(keepAlive);process.exit(code??1);}
 };
 child.once('error',()=>finish(null));child.once('close',finish);
});
process.send?.({type:'ready'},()=>{});
