import {validateComponent,type JsonValue,type SurfaceValidationOptions,type SurfaceValidationResult} from '@qian/surfaces';

/**
 * CLI 离线校验组件。CLI 拿不到宿主的完整能力目录，所以把组件自身工具里本命名空间（`<组件ID>/…`）的 command 绑定视为该组件已登记的能力：
 * 带代码组件的 tools 由 `[Capability]` 派生，仓库清单门禁核对它与代码一致，平台宿主按制品绑定时还会以完整目录复核。跨组件的 command 引用仍报错。
 */
export function validateOfflineComponent(directory:string,options:SurfaceValidationOptions):SurfaceValidationResult {
 const first=validateComponent(directory,options),component=first.component;
 if(!component)return first;
 const own=component.documents.filter(doc=>doc.kind==='tool').map(doc=>object(object(doc.data).binding))
  .filter(binding=>binding.kind==='command'&&typeof binding.id==='string'&&binding.id.startsWith(component.id+'/')).map(binding=>String(binding.id));
 return own.length?validateComponent(directory,{...options,capabilities:[...options.capabilities??[],...own]}):first;
}

const object=(value:JsonValue|undefined):Record<string,JsonValue>=>value!==null&&typeof value==='object'&&!Array.isArray(value)?value as Record<string,JsonValue>:{};
