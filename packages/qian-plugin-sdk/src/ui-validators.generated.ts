// 由 schemas/v2/ui.schema.json 经 Ajv standalone 预编译（contribution.parameters 不含元 Schema 引用，见 src/parameters.ts）；禁止手改。运行 scripts/ui-schema.mjs。
/* eslint-disable */
// @ts-nocheck
// Ajv 运行时辅助（与 ajv/dist/runtime/ucs2length、equal 同义）：按 Unicode 码点计长；JSON 值的深相等。
function ucs2length(str) { let length = 0; for (let pos = 0; pos < str.length; length++) { const value = str.charCodeAt(pos++); if (value >= 0xd800 && value <= 0xdbff && pos < str.length && (str.charCodeAt(pos) & 0xfc00) === 0xdc00) pos++; } return length; }
function jsonEqual(a, b) {
  if (a === b) return true;
  if (!a || !b || typeof a !== 'object' || typeof b !== 'object' || Array.isArray(a) !== Array.isArray(b)) return a !== a && b !== b;
  const keys = Object.keys(a);
  if (keys.length !== Object.keys(b).length) return false;
  return keys.every((key) => Object.prototype.hasOwnProperty.call(b, key) && jsonEqual(a[key], b[key]));
}
export const validateUiManifest = validate20;
const schema31 = {"$schema":"https://json-schema.org/draft/2020-12/schema","$id":"https://qian.dev/schemas/ui/v2","title":"Qian 组件 UI 表面","description":"静态 UI 描述；桥接授权为显式可选扩展，contributions 必须声明 bridge。浏览器运行时可进一步拒绝尚未实现的表面。","type":"object","additionalProperties":false,"required":["$schema","federation","engines"],"properties":{"$schema":{"const":"https://qian.dev/schemas/ui/v2"},"federation":{"type":"object","additionalProperties":false,"required":["manifest","exposes"],"properties":{"manifest":{"type":"string","minLength":1,"maxLength":2048},"exposes":{"type":"array","minItems":1,"uniqueItems":true,"items":{"$ref":"#/$defs/expose"}}}},"engines":{"type":"object","additionalProperties":false,"required":["yue"],"properties":{"yue":{"$ref":"#/$defs/text"}}},"pages":{"type":"array","items":{"$ref":"#/$defs/page"}},"widgets":{"type":"array","items":{"$ref":"#/$defs/widget"}},"settingsCards":{"type":"array","items":{"$ref":"#/$defs/settingsCard"}},"contributions":{"type":"array","items":{"$ref":"#/$defs/contribution"}},"bridge":{"type":"object","additionalProperties":false,"required":["slots","navigate","notify","capabilities"],"properties":{"slots":{"type":"array","items":{"$ref":"#/$defs/slotId"},"uniqueItems":true},"navigate":{"type":"array","items":{"$ref":"#/$defs/route"},"uniqueItems":true},"notify":{"type":"boolean"},"capabilities":{"type":"array","items":{"$ref":"#/$defs/bridgeCapability"}}}}},"allOf":[{"if":{"required":["contributions"],"properties":{"contributions":{}}},"then":{"required":["bridge"],"properties":{"bridge":{}}}}],"$defs":{"text":{"type":"string","minLength":1,"maxLength":256},"id":{"type":"string","minLength":1,"maxLength":256,"pattern":"^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$"},"expose":{"type":"string","minLength":1,"maxLength":256,"pattern":"^[A-Za-z][A-Za-z0-9_-]*$"},"route":{"type":"string","minLength":1,"maxLength":256,"pattern":"^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"},"slotId":{"enum":["qian.shell.nav/v1","qian.shell.settings/v1","qian.shell.user-menu/v1","qian.shell.command/v1","qian.shell.preview/v1","qian.shell.client-action/v1"]},"contribution":{"type":"object","additionalProperties":false,"required":["slot","id","title","description","permission","expose"],"properties":{"slot":{"$ref":"#/$defs/slotId"},"id":{"$ref":"#/$defs/id"},"title":{"$ref":"#/$defs/text"},"description":{"$ref":"#/$defs/text"},"permission":{"$ref":"#/$defs/text"},"expose":{"$ref":"#/$defs/expose"},"route":{"$ref":"#/$defs/route"},"parameters":{"type":"object"}},"allOf":[{"if":{"properties":{"slot":{"const":"qian.shell.nav/v1"}},"required":["slot"]},"then":{"required":["route"],"properties":{"route":{}}}},{"if":{"properties":{"slot":{"const":"qian.shell.client-action/v1"}},"required":["slot"]},"then":{"required":["parameters"],"properties":{"parameters":{}}}}]},"page":{"type":"object","additionalProperties":false,"required":["route","expose","permission","nav"],"properties":{"route":{"$ref":"#/$defs/route"},"expose":{"$ref":"#/$defs/expose"},"permission":{"$ref":"#/$defs/text"},"nav":{"type":"object","additionalProperties":false,"required":["title"],"properties":{"title":{"$ref":"#/$defs/text"},"icon":{"$ref":"#/$defs/text"},"order":{"type":"integer"}}}}},"widget":{"type":"object","additionalProperties":false,"required":["type","expose"],"properties":{"type":{"$ref":"#/$defs/text"},"expose":{"$ref":"#/$defs/expose"}}},"settingsCard":{"type":"object","additionalProperties":false,"required":["expose","scope","permission"],"properties":{"expose":{"$ref":"#/$defs/expose"},"scope":{"enum":["global","tenant","app","agent-session"]},"permission":{"$ref":"#/$defs/text"}}},"bridgeCapability":{"type":"object","additionalProperties":false,"required":["id","permission","sideEffect"],"properties":{"id":{"type":"string","minLength":1,"maxLength":256,"pattern":"^[a-z][a-z0-9.]+/[A-Za-z0-9._-]+$"},"permission":{"$ref":"#/$defs/text"},"sideEffect":{"enum":["read","write"]}}}}};
const schema32 = {"type":"string","minLength":1,"maxLength":256,"pattern":"^[A-Za-z][A-Za-z0-9_-]*$"};
const schema33 = {"type":"string","minLength":1,"maxLength":256};
const schema47 = {"enum":["qian.shell.nav/v1","qian.shell.settings/v1","qian.shell.user-menu/v1","qian.shell.command/v1","qian.shell.preview/v1","qian.shell.client-action/v1"]};
const schema35 = {"type":"string","minLength":1,"maxLength":256,"pattern":"^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"};
const func1 = ucs2length;
const func0 = jsonEqual;
const pattern4 = new RegExp("^[A-Za-z][A-Za-z0-9_-]*$", "u");
const pattern5 = new RegExp("^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$", "u");
const schema34 = {"type":"object","additionalProperties":false,"required":["route","expose","permission","nav"],"properties":{"route":{"$ref":"#/$defs/route"},"expose":{"$ref":"#/$defs/expose"},"permission":{"$ref":"#/$defs/text"},"nav":{"type":"object","additionalProperties":false,"required":["title"],"properties":{"title":{"$ref":"#/$defs/text"},"icon":{"$ref":"#/$defs/text"},"order":{"type":"integer"}}}}};

function validate21(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate21.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.route === undefined){
const err0 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "route"},message:"must have required property '"+"route"+"'"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
if(data.expose === undefined){
const err1 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "expose"},message:"must have required property '"+"expose"+"'"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
if(data.permission === undefined){
const err2 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "permission"},message:"must have required property '"+"permission"+"'"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
if(data.nav === undefined){
const err3 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "nav"},message:"must have required property '"+"nav"+"'"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
for(const key0 in data){
if(!((((key0 === "route") || (key0 === "expose")) || (key0 === "permission")) || (key0 === "nav"))){
const err4 = {instancePath,schemaPath:"#/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key0},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
}
if(data.route !== undefined){
let data0 = data.route;
if(typeof data0 === "string"){
if(func1(data0) > 256){
const err5 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
if(func1(data0) < 1){
const err6 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
if(!pattern5.test(data0)){
const err7 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/pattern",keyword:"pattern",params:{pattern: "^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"},message:"must match pattern \""+"^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"+"\""};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
}
}
else {
const err8 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
}
if(data.expose !== undefined){
let data1 = data.expose;
if(typeof data1 === "string"){
if(func1(data1) > 256){
const err9 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
}
if(func1(data1) < 1){
const err10 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
if(!pattern4.test(data1)){
const err11 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z][A-Za-z0-9_-]*$"},message:"must match pattern \""+"^[A-Za-z][A-Za-z0-9_-]*$"+"\""};
if(vErrors === null){
vErrors = [err11];
}
else {
vErrors.push(err11);
}
errors++;
}
}
else {
const err12 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err12];
}
else {
vErrors.push(err12);
}
errors++;
}
}
if(data.permission !== undefined){
let data2 = data.permission;
if(typeof data2 === "string"){
if(func1(data2) > 256){
const err13 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err13];
}
else {
vErrors.push(err13);
}
errors++;
}
if(func1(data2) < 1){
const err14 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err14];
}
else {
vErrors.push(err14);
}
errors++;
}
}
else {
const err15 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err15];
}
else {
vErrors.push(err15);
}
errors++;
}
}
if(data.nav !== undefined){
let data3 = data.nav;
if(data3 && typeof data3 == "object" && !Array.isArray(data3)){
if(data3.title === undefined){
const err16 = {instancePath:instancePath+"/nav",schemaPath:"#/properties/nav/required",keyword:"required",params:{missingProperty: "title"},message:"must have required property '"+"title"+"'"};
if(vErrors === null){
vErrors = [err16];
}
else {
vErrors.push(err16);
}
errors++;
}
for(const key1 in data3){
if(!(((key1 === "title") || (key1 === "icon")) || (key1 === "order"))){
const err17 = {instancePath:instancePath+"/nav",schemaPath:"#/properties/nav/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key1},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err17];
}
else {
vErrors.push(err17);
}
errors++;
}
}
if(data3.title !== undefined){
let data4 = data3.title;
if(typeof data4 === "string"){
if(func1(data4) > 256){
const err18 = {instancePath:instancePath+"/nav/title",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err18];
}
else {
vErrors.push(err18);
}
errors++;
}
if(func1(data4) < 1){
const err19 = {instancePath:instancePath+"/nav/title",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err19];
}
else {
vErrors.push(err19);
}
errors++;
}
}
else {
const err20 = {instancePath:instancePath+"/nav/title",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err20];
}
else {
vErrors.push(err20);
}
errors++;
}
}
if(data3.icon !== undefined){
let data5 = data3.icon;
if(typeof data5 === "string"){
if(func1(data5) > 256){
const err21 = {instancePath:instancePath+"/nav/icon",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err21];
}
else {
vErrors.push(err21);
}
errors++;
}
if(func1(data5) < 1){
const err22 = {instancePath:instancePath+"/nav/icon",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err22];
}
else {
vErrors.push(err22);
}
errors++;
}
}
else {
const err23 = {instancePath:instancePath+"/nav/icon",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err23];
}
else {
vErrors.push(err23);
}
errors++;
}
}
if(data3.order !== undefined){
let data6 = data3.order;
if(!(((typeof data6 == "number") && (!(data6 % 1) && !isNaN(data6))) && (isFinite(data6)))){
const err24 = {instancePath:instancePath+"/nav/order",schemaPath:"#/properties/nav/properties/order/type",keyword:"type",params:{type: "integer"},message:"must be integer"};
if(vErrors === null){
vErrors = [err24];
}
else {
vErrors.push(err24);
}
errors++;
}
}
}
else {
const err25 = {instancePath:instancePath+"/nav",schemaPath:"#/properties/nav/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err25];
}
else {
vErrors.push(err25);
}
errors++;
}
}
}
else {
const err26 = {instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err26];
}
else {
vErrors.push(err26);
}
errors++;
}
validate21.errors = vErrors;
return errors === 0;
}
validate21.evaluated = {"props":true,"dynamicProps":false,"dynamicItems":false};

const schema40 = {"type":"object","additionalProperties":false,"required":["type","expose"],"properties":{"type":{"$ref":"#/$defs/text"},"expose":{"$ref":"#/$defs/expose"}}};

function validate23(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate23.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.type === undefined){
const err0 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "type"},message:"must have required property '"+"type"+"'"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
if(data.expose === undefined){
const err1 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "expose"},message:"must have required property '"+"expose"+"'"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
for(const key0 in data){
if(!((key0 === "type") || (key0 === "expose"))){
const err2 = {instancePath,schemaPath:"#/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key0},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
}
if(data.type !== undefined){
let data0 = data.type;
if(typeof data0 === "string"){
if(func1(data0) > 256){
const err3 = {instancePath:instancePath+"/type",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
if(func1(data0) < 1){
const err4 = {instancePath:instancePath+"/type",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
}
else {
const err5 = {instancePath:instancePath+"/type",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
}
if(data.expose !== undefined){
let data1 = data.expose;
if(typeof data1 === "string"){
if(func1(data1) > 256){
const err6 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
if(func1(data1) < 1){
const err7 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
}
if(!pattern4.test(data1)){
const err8 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z][A-Za-z0-9_-]*$"},message:"must match pattern \""+"^[A-Za-z][A-Za-z0-9_-]*$"+"\""};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
}
else {
const err9 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
}
}
}
else {
const err10 = {instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
validate23.errors = vErrors;
return errors === 0;
}
validate23.evaluated = {"props":true,"dynamicProps":false,"dynamicItems":false};

const schema43 = {"type":"object","additionalProperties":false,"required":["expose","scope","permission"],"properties":{"expose":{"$ref":"#/$defs/expose"},"scope":{"enum":["global","tenant","app","agent-session"]},"permission":{"$ref":"#/$defs/text"}}};

function validate25(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate25.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.expose === undefined){
const err0 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "expose"},message:"must have required property '"+"expose"+"'"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
if(data.scope === undefined){
const err1 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "scope"},message:"must have required property '"+"scope"+"'"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
if(data.permission === undefined){
const err2 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "permission"},message:"must have required property '"+"permission"+"'"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
for(const key0 in data){
if(!(((key0 === "expose") || (key0 === "scope")) || (key0 === "permission"))){
const err3 = {instancePath,schemaPath:"#/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key0},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
}
if(data.expose !== undefined){
let data0 = data.expose;
if(typeof data0 === "string"){
if(func1(data0) > 256){
const err4 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
if(func1(data0) < 1){
const err5 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
if(!pattern4.test(data0)){
const err6 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z][A-Za-z0-9_-]*$"},message:"must match pattern \""+"^[A-Za-z][A-Za-z0-9_-]*$"+"\""};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
}
else {
const err7 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
}
}
if(data.scope !== undefined){
let data1 = data.scope;
if(!((((data1 === "global") || (data1 === "tenant")) || (data1 === "app")) || (data1 === "agent-session"))){
const err8 = {instancePath:instancePath+"/scope",schemaPath:"#/properties/scope/enum",keyword:"enum",params:{allowedValues: schema43.properties.scope.enum},message:"must be equal to one of the allowed values"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
}
if(data.permission !== undefined){
let data2 = data.permission;
if(typeof data2 === "string"){
if(func1(data2) > 256){
const err9 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
}
if(func1(data2) < 1){
const err10 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
}
else {
const err11 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err11];
}
else {
vErrors.push(err11);
}
errors++;
}
}
}
else {
const err12 = {instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err12];
}
else {
vErrors.push(err12);
}
errors++;
}
validate25.errors = vErrors;
return errors === 0;
}
validate25.evaluated = {"props":true,"dynamicProps":false,"dynamicItems":false};

const schema46 = {"type":"object","additionalProperties":false,"required":["slot","id","title","description","permission","expose"],"properties":{"slot":{"$ref":"#/$defs/slotId"},"id":{"$ref":"#/$defs/id"},"title":{"$ref":"#/$defs/text"},"description":{"$ref":"#/$defs/text"},"permission":{"$ref":"#/$defs/text"},"expose":{"$ref":"#/$defs/expose"},"route":{"$ref":"#/$defs/route"},"parameters":{"type":"object"}},"allOf":[{"if":{"properties":{"slot":{"const":"qian.shell.nav/v1"}},"required":["slot"]},"then":{"required":["route"],"properties":{"route":{}}}},{"if":{"properties":{"slot":{"const":"qian.shell.client-action/v1"}},"required":["slot"]},"then":{"required":["parameters"],"properties":{"parameters":{}}}}]};
const schema48 = {"type":"string","minLength":1,"maxLength":256,"pattern":"^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$"};
const pattern9 = new RegExp("^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$", "u");

function validate27(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate27.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs2 = errors;
let valid1 = true;
const _errs3 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((data.slot === undefined) && (missing0 = "slot")){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
else {
if(data.slot !== undefined){
if("qian.shell.nav/v1" !== data.slot){
const err1 = {};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
}
}
}
var _valid0 = _errs3 === errors;
errors = _errs2;
if(vErrors !== null){
if(_errs2){
vErrors.length = _errs2;
}
else {
vErrors = null;
}
}
if(_valid0){
const _errs5 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.route === undefined){
const err2 = {instancePath,schemaPath:"#/allOf/0/then/required",keyword:"required",params:{missingProperty: "route"},message:"must have required property '"+"route"+"'"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
}
var _valid0 = _errs5 === errors;
valid1 = _valid0;
if(valid1){
var props0 = {};
props0.route = true;
props0.slot = true;
}
}
if(!valid1){
const err3 = {instancePath,schemaPath:"#/allOf/0/if",keyword:"if",params:{failingKeyword: "then"},message:"must match \"then\" schema"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
const _errs7 = errors;
let valid3 = true;
const _errs8 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
let missing1;
if((data.slot === undefined) && (missing1 = "slot")){
const err4 = {};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
else {
if(data.slot !== undefined){
if("qian.shell.client-action/v1" !== data.slot){
const err5 = {};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
}
}
}
var _valid1 = _errs8 === errors;
errors = _errs7;
if(vErrors !== null){
if(_errs7){
vErrors.length = _errs7;
}
else {
vErrors = null;
}
}
if(_valid1){
const _errs10 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.parameters === undefined){
const err6 = {instancePath,schemaPath:"#/allOf/1/then/required",keyword:"required",params:{missingProperty: "parameters"},message:"must have required property '"+"parameters"+"'"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
}
var _valid1 = _errs10 === errors;
valid3 = _valid1;
if(valid3){
var props1 = {};
props1.parameters = true;
props1.slot = true;
}
}
if(!valid3){
const err7 = {instancePath,schemaPath:"#/allOf/1/if",keyword:"if",params:{failingKeyword: "then"},message:"must match \"then\" schema"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
}
if(props0 !== true && props1 !== undefined){
if(props1 === true){
props0 = true;
}
else {
props0 = props0 || {};
Object.assign(props0, props1);
}
}
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.slot === undefined){
const err8 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "slot"},message:"must have required property '"+"slot"+"'"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
if(data.id === undefined){
const err9 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "id"},message:"must have required property '"+"id"+"'"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
}
if(data.title === undefined){
const err10 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "title"},message:"must have required property '"+"title"+"'"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
if(data.description === undefined){
const err11 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "description"},message:"must have required property '"+"description"+"'"};
if(vErrors === null){
vErrors = [err11];
}
else {
vErrors.push(err11);
}
errors++;
}
if(data.permission === undefined){
const err12 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "permission"},message:"must have required property '"+"permission"+"'"};
if(vErrors === null){
vErrors = [err12];
}
else {
vErrors.push(err12);
}
errors++;
}
if(data.expose === undefined){
const err13 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "expose"},message:"must have required property '"+"expose"+"'"};
if(vErrors === null){
vErrors = [err13];
}
else {
vErrors.push(err13);
}
errors++;
}
for(const key0 in data){
if(!((((((((key0 === "slot") || (key0 === "id")) || (key0 === "title")) || (key0 === "description")) || (key0 === "permission")) || (key0 === "expose")) || (key0 === "route")) || (key0 === "parameters"))){
const err14 = {instancePath,schemaPath:"#/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key0},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err14];
}
else {
vErrors.push(err14);
}
errors++;
}
}
if(data.slot !== undefined){
let data2 = data.slot;
if(!((((((data2 === "qian.shell.nav/v1") || (data2 === "qian.shell.settings/v1")) || (data2 === "qian.shell.user-menu/v1")) || (data2 === "qian.shell.command/v1")) || (data2 === "qian.shell.preview/v1")) || (data2 === "qian.shell.client-action/v1"))){
const err15 = {instancePath:instancePath+"/slot",schemaPath:"#/$defs/slotId/enum",keyword:"enum",params:{allowedValues: schema47.enum},message:"must be equal to one of the allowed values"};
if(vErrors === null){
vErrors = [err15];
}
else {
vErrors.push(err15);
}
errors++;
}
}
if(data.id !== undefined){
let data3 = data.id;
if(typeof data3 === "string"){
if(func1(data3) > 256){
const err16 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err16];
}
else {
vErrors.push(err16);
}
errors++;
}
if(func1(data3) < 1){
const err17 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err17];
}
else {
vErrors.push(err17);
}
errors++;
}
if(!pattern9.test(data3)){
const err18 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/pattern",keyword:"pattern",params:{pattern: "^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$"},message:"must match pattern \""+"^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$"+"\""};
if(vErrors === null){
vErrors = [err18];
}
else {
vErrors.push(err18);
}
errors++;
}
}
else {
const err19 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err19];
}
else {
vErrors.push(err19);
}
errors++;
}
}
if(data.title !== undefined){
let data4 = data.title;
if(typeof data4 === "string"){
if(func1(data4) > 256){
const err20 = {instancePath:instancePath+"/title",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err20];
}
else {
vErrors.push(err20);
}
errors++;
}
if(func1(data4) < 1){
const err21 = {instancePath:instancePath+"/title",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err21];
}
else {
vErrors.push(err21);
}
errors++;
}
}
else {
const err22 = {instancePath:instancePath+"/title",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err22];
}
else {
vErrors.push(err22);
}
errors++;
}
}
if(data.description !== undefined){
let data5 = data.description;
if(typeof data5 === "string"){
if(func1(data5) > 256){
const err23 = {instancePath:instancePath+"/description",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err23];
}
else {
vErrors.push(err23);
}
errors++;
}
if(func1(data5) < 1){
const err24 = {instancePath:instancePath+"/description",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err24];
}
else {
vErrors.push(err24);
}
errors++;
}
}
else {
const err25 = {instancePath:instancePath+"/description",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err25];
}
else {
vErrors.push(err25);
}
errors++;
}
}
if(data.permission !== undefined){
let data6 = data.permission;
if(typeof data6 === "string"){
if(func1(data6) > 256){
const err26 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err26];
}
else {
vErrors.push(err26);
}
errors++;
}
if(func1(data6) < 1){
const err27 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err27];
}
else {
vErrors.push(err27);
}
errors++;
}
}
else {
const err28 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err28];
}
else {
vErrors.push(err28);
}
errors++;
}
}
if(data.expose !== undefined){
let data7 = data.expose;
if(typeof data7 === "string"){
if(func1(data7) > 256){
const err29 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err29];
}
else {
vErrors.push(err29);
}
errors++;
}
if(func1(data7) < 1){
const err30 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err30];
}
else {
vErrors.push(err30);
}
errors++;
}
if(!pattern4.test(data7)){
const err31 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z][A-Za-z0-9_-]*$"},message:"must match pattern \""+"^[A-Za-z][A-Za-z0-9_-]*$"+"\""};
if(vErrors === null){
vErrors = [err31];
}
else {
vErrors.push(err31);
}
errors++;
}
}
else {
const err32 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err32];
}
else {
vErrors.push(err32);
}
errors++;
}
}
if(data.route !== undefined){
let data8 = data.route;
if(typeof data8 === "string"){
if(func1(data8) > 256){
const err33 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err33];
}
else {
vErrors.push(err33);
}
errors++;
}
if(func1(data8) < 1){
const err34 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err34];
}
else {
vErrors.push(err34);
}
errors++;
}
if(!pattern5.test(data8)){
const err35 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/pattern",keyword:"pattern",params:{pattern: "^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"},message:"must match pattern \""+"^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"+"\""};
if(vErrors === null){
vErrors = [err35];
}
else {
vErrors.push(err35);
}
errors++;
}
}
else {
const err36 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err36];
}
else {
vErrors.push(err36);
}
errors++;
}
}
if(data.parameters !== undefined){
let data9 = data.parameters;
if(!(data9 && typeof data9 == "object" && !Array.isArray(data9))){
const err37 = {instancePath:instancePath+"/parameters",schemaPath:"#/properties/parameters/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err37];
}
else {
vErrors.push(err37);
}
errors++;
}
}
}
else {
const err38 = {instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err38];
}
else {
vErrors.push(err38);
}
errors++;
}
validate27.errors = vErrors;
return errors === 0;
}
validate27.evaluated = {"props":true,"dynamicProps":false,"dynamicItems":false};

const schema56 = {"type":"object","additionalProperties":false,"required":["id","permission","sideEffect"],"properties":{"id":{"type":"string","minLength":1,"maxLength":256,"pattern":"^[a-z][a-z0-9.]+/[A-Za-z0-9._-]+$"},"permission":{"$ref":"#/$defs/text"},"sideEffect":{"enum":["read","write"]}}};
const pattern13 = new RegExp("^[a-z][a-z0-9.]+/[A-Za-z0-9._-]+$", "u");

function validate29(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate29.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.id === undefined){
const err0 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "id"},message:"must have required property '"+"id"+"'"};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
if(data.permission === undefined){
const err1 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "permission"},message:"must have required property '"+"permission"+"'"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
if(data.sideEffect === undefined){
const err2 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "sideEffect"},message:"must have required property '"+"sideEffect"+"'"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
for(const key0 in data){
if(!(((key0 === "id") || (key0 === "permission")) || (key0 === "sideEffect"))){
const err3 = {instancePath,schemaPath:"#/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key0},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
}
if(data.id !== undefined){
let data0 = data.id;
if(typeof data0 === "string"){
if(func1(data0) > 256){
const err4 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
if(func1(data0) < 1){
const err5 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
if(!pattern13.test(data0)){
const err6 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/pattern",keyword:"pattern",params:{pattern: "^[a-z][a-z0-9.]+/[A-Za-z0-9._-]+$"},message:"must match pattern \""+"^[a-z][a-z0-9.]+/[A-Za-z0-9._-]+$"+"\""};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
}
else {
const err7 = {instancePath:instancePath+"/id",schemaPath:"#/properties/id/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
}
}
if(data.permission !== undefined){
let data1 = data.permission;
if(typeof data1 === "string"){
if(func1(data1) > 256){
const err8 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
if(func1(data1) < 1){
const err9 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
}
}
else {
const err10 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
}
if(data.sideEffect !== undefined){
let data2 = data.sideEffect;
if(!((data2 === "read") || (data2 === "write"))){
const err11 = {instancePath:instancePath+"/sideEffect",schemaPath:"#/properties/sideEffect/enum",keyword:"enum",params:{allowedValues: schema56.properties.sideEffect.enum},message:"must be equal to one of the allowed values"};
if(vErrors === null){
vErrors = [err11];
}
else {
vErrors.push(err11);
}
errors++;
}
}
}
else {
const err12 = {instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err12];
}
else {
vErrors.push(err12);
}
errors++;
}
validate29.errors = vErrors;
return errors === 0;
}
validate29.evaluated = {"props":true,"dynamicProps":false,"dynamicItems":false};


function validate20(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
/*# sourceURL="https://qian.dev/schemas/ui/v2" */;
let vErrors = null;
let errors = 0;
const evaluated0 = validate20.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs2 = errors;
let valid1 = true;
const _errs3 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((data.contributions === undefined) && (missing0 = "contributions")){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
}
var _valid0 = _errs3 === errors;
errors = _errs2;
if(vErrors !== null){
if(_errs2){
vErrors.length = _errs2;
}
else {
vErrors = null;
}
}
if(_valid0){
const _errs4 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.bridge === undefined){
const err1 = {instancePath,schemaPath:"#/allOf/0/then/required",keyword:"required",params:{missingProperty: "bridge"},message:"must have required property '"+"bridge"+"'"};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
}
var _valid0 = _errs4 === errors;
valid1 = _valid0;
if(valid1){
var props0 = {};
props0.bridge = true;
props0.contributions = true;
}
}
if(!valid1){
const err2 = {instancePath,schemaPath:"#/allOf/0/if",keyword:"if",params:{failingKeyword: "then"},message:"must match \"then\" schema"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.$schema === undefined){
const err3 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "$schema"},message:"must have required property '"+"$schema"+"'"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
if(data.federation === undefined){
const err4 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "federation"},message:"must have required property '"+"federation"+"'"};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
if(data.engines === undefined){
const err5 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "engines"},message:"must have required property '"+"engines"+"'"};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
for(const key0 in data){
if(!((((((((key0 === "$schema") || (key0 === "federation")) || (key0 === "engines")) || (key0 === "pages")) || (key0 === "widgets")) || (key0 === "settingsCards")) || (key0 === "contributions")) || (key0 === "bridge"))){
const err6 = {instancePath,schemaPath:"#/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key0},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
}
if(data.$schema !== undefined){
if("https://qian.dev/schemas/ui/v2" !== data.$schema){
const err7 = {instancePath:instancePath+"/$schema",schemaPath:"#/properties/%24schema/const",keyword:"const",params:{allowedValue: "https://qian.dev/schemas/ui/v2"},message:"must be equal to constant"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
}
}
if(data.federation !== undefined){
let data1 = data.federation;
if(data1 && typeof data1 == "object" && !Array.isArray(data1)){
if(data1.manifest === undefined){
const err8 = {instancePath:instancePath+"/federation",schemaPath:"#/properties/federation/required",keyword:"required",params:{missingProperty: "manifest"},message:"must have required property '"+"manifest"+"'"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
if(data1.exposes === undefined){
const err9 = {instancePath:instancePath+"/federation",schemaPath:"#/properties/federation/required",keyword:"required",params:{missingProperty: "exposes"},message:"must have required property '"+"exposes"+"'"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
}
for(const key1 in data1){
if(!((key1 === "manifest") || (key1 === "exposes"))){
const err10 = {instancePath:instancePath+"/federation",schemaPath:"#/properties/federation/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key1},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
}
if(data1.manifest !== undefined){
let data2 = data1.manifest;
if(typeof data2 === "string"){
if(func1(data2) > 2048){
const err11 = {instancePath:instancePath+"/federation/manifest",schemaPath:"#/properties/federation/properties/manifest/maxLength",keyword:"maxLength",params:{limit: 2048},message:"must NOT have more than 2048 characters"};
if(vErrors === null){
vErrors = [err11];
}
else {
vErrors.push(err11);
}
errors++;
}
if(func1(data2) < 1){
const err12 = {instancePath:instancePath+"/federation/manifest",schemaPath:"#/properties/federation/properties/manifest/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err12];
}
else {
vErrors.push(err12);
}
errors++;
}
}
else {
const err13 = {instancePath:instancePath+"/federation/manifest",schemaPath:"#/properties/federation/properties/manifest/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err13];
}
else {
vErrors.push(err13);
}
errors++;
}
}
if(data1.exposes !== undefined){
let data3 = data1.exposes;
if(Array.isArray(data3)){
if(data3.length < 1){
const err14 = {instancePath:instancePath+"/federation/exposes",schemaPath:"#/properties/federation/properties/exposes/minItems",keyword:"minItems",params:{limit: 1},message:"must NOT have fewer than 1 items"};
if(vErrors === null){
vErrors = [err14];
}
else {
vErrors.push(err14);
}
errors++;
}
const len0 = data3.length;
for(let i0=0; i0<len0; i0++){
let data4 = data3[i0];
if(typeof data4 === "string"){
if(func1(data4) > 256){
const err15 = {instancePath:instancePath+"/federation/exposes/" + i0,schemaPath:"#/$defs/expose/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err15];
}
else {
vErrors.push(err15);
}
errors++;
}
if(func1(data4) < 1){
const err16 = {instancePath:instancePath+"/federation/exposes/" + i0,schemaPath:"#/$defs/expose/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err16];
}
else {
vErrors.push(err16);
}
errors++;
}
if(!pattern4.test(data4)){
const err17 = {instancePath:instancePath+"/federation/exposes/" + i0,schemaPath:"#/$defs/expose/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z][A-Za-z0-9_-]*$"},message:"must match pattern \""+"^[A-Za-z][A-Za-z0-9_-]*$"+"\""};
if(vErrors === null){
vErrors = [err17];
}
else {
vErrors.push(err17);
}
errors++;
}
}
else {
const err18 = {instancePath:instancePath+"/federation/exposes/" + i0,schemaPath:"#/$defs/expose/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err18];
}
else {
vErrors.push(err18);
}
errors++;
}
}
let i1 = data3.length;
let j0;
if(i1 > 1){
outer0:
for(;i1--;){
for(j0 = i1; j0--;){
if(func0(data3[i1], data3[j0])){
const err19 = {instancePath:instancePath+"/federation/exposes",schemaPath:"#/properties/federation/properties/exposes/uniqueItems",keyword:"uniqueItems",params:{i: i1, j: j0},message:"must NOT have duplicate items (items ## "+j0+" and "+i1+" are identical)"};
if(vErrors === null){
vErrors = [err19];
}
else {
vErrors.push(err19);
}
errors++;
break outer0;
}
}
}
}
}
else {
const err20 = {instancePath:instancePath+"/federation/exposes",schemaPath:"#/properties/federation/properties/exposes/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err20];
}
else {
vErrors.push(err20);
}
errors++;
}
}
}
else {
const err21 = {instancePath:instancePath+"/federation",schemaPath:"#/properties/federation/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err21];
}
else {
vErrors.push(err21);
}
errors++;
}
}
if(data.engines !== undefined){
let data5 = data.engines;
if(data5 && typeof data5 == "object" && !Array.isArray(data5)){
if(data5.yue === undefined){
const err22 = {instancePath:instancePath+"/engines",schemaPath:"#/properties/engines/required",keyword:"required",params:{missingProperty: "yue"},message:"must have required property '"+"yue"+"'"};
if(vErrors === null){
vErrors = [err22];
}
else {
vErrors.push(err22);
}
errors++;
}
for(const key2 in data5){
if(!(key2 === "yue")){
const err23 = {instancePath:instancePath+"/engines",schemaPath:"#/properties/engines/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key2},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err23];
}
else {
vErrors.push(err23);
}
errors++;
}
}
if(data5.yue !== undefined){
let data6 = data5.yue;
if(typeof data6 === "string"){
if(func1(data6) > 256){
const err24 = {instancePath:instancePath+"/engines/yue",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err24];
}
else {
vErrors.push(err24);
}
errors++;
}
if(func1(data6) < 1){
const err25 = {instancePath:instancePath+"/engines/yue",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err25];
}
else {
vErrors.push(err25);
}
errors++;
}
}
else {
const err26 = {instancePath:instancePath+"/engines/yue",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err26];
}
else {
vErrors.push(err26);
}
errors++;
}
}
}
else {
const err27 = {instancePath:instancePath+"/engines",schemaPath:"#/properties/engines/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err27];
}
else {
vErrors.push(err27);
}
errors++;
}
}
if(data.pages !== undefined){
let data7 = data.pages;
if(Array.isArray(data7)){
const len1 = data7.length;
for(let i2=0; i2<len1; i2++){
if(!(validate21(data7[i2], {instancePath:instancePath+"/pages/" + i2,parentData:data7,parentDataProperty:i2,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate21.errors : vErrors.concat(validate21.errors);
errors = vErrors.length;
}
}
}
else {
const err28 = {instancePath:instancePath+"/pages",schemaPath:"#/properties/pages/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err28];
}
else {
vErrors.push(err28);
}
errors++;
}
}
if(data.widgets !== undefined){
let data9 = data.widgets;
if(Array.isArray(data9)){
const len2 = data9.length;
for(let i3=0; i3<len2; i3++){
if(!(validate23(data9[i3], {instancePath:instancePath+"/widgets/" + i3,parentData:data9,parentDataProperty:i3,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate23.errors : vErrors.concat(validate23.errors);
errors = vErrors.length;
}
}
}
else {
const err29 = {instancePath:instancePath+"/widgets",schemaPath:"#/properties/widgets/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err29];
}
else {
vErrors.push(err29);
}
errors++;
}
}
if(data.settingsCards !== undefined){
let data11 = data.settingsCards;
if(Array.isArray(data11)){
const len3 = data11.length;
for(let i4=0; i4<len3; i4++){
if(!(validate25(data11[i4], {instancePath:instancePath+"/settingsCards/" + i4,parentData:data11,parentDataProperty:i4,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate25.errors : vErrors.concat(validate25.errors);
errors = vErrors.length;
}
}
}
else {
const err30 = {instancePath:instancePath+"/settingsCards",schemaPath:"#/properties/settingsCards/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err30];
}
else {
vErrors.push(err30);
}
errors++;
}
}
if(data.contributions !== undefined){
let data13 = data.contributions;
if(Array.isArray(data13)){
const len4 = data13.length;
for(let i5=0; i5<len4; i5++){
if(!(validate27(data13[i5], {instancePath:instancePath+"/contributions/" + i5,parentData:data13,parentDataProperty:i5,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate27.errors : vErrors.concat(validate27.errors);
errors = vErrors.length;
}
}
}
else {
const err31 = {instancePath:instancePath+"/contributions",schemaPath:"#/properties/contributions/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err31];
}
else {
vErrors.push(err31);
}
errors++;
}
}
if(data.bridge !== undefined){
let data15 = data.bridge;
if(data15 && typeof data15 == "object" && !Array.isArray(data15)){
if(data15.slots === undefined){
const err32 = {instancePath:instancePath+"/bridge",schemaPath:"#/properties/bridge/required",keyword:"required",params:{missingProperty: "slots"},message:"must have required property '"+"slots"+"'"};
if(vErrors === null){
vErrors = [err32];
}
else {
vErrors.push(err32);
}
errors++;
}
if(data15.navigate === undefined){
const err33 = {instancePath:instancePath+"/bridge",schemaPath:"#/properties/bridge/required",keyword:"required",params:{missingProperty: "navigate"},message:"must have required property '"+"navigate"+"'"};
if(vErrors === null){
vErrors = [err33];
}
else {
vErrors.push(err33);
}
errors++;
}
if(data15.notify === undefined){
const err34 = {instancePath:instancePath+"/bridge",schemaPath:"#/properties/bridge/required",keyword:"required",params:{missingProperty: "notify"},message:"must have required property '"+"notify"+"'"};
if(vErrors === null){
vErrors = [err34];
}
else {
vErrors.push(err34);
}
errors++;
}
if(data15.capabilities === undefined){
const err35 = {instancePath:instancePath+"/bridge",schemaPath:"#/properties/bridge/required",keyword:"required",params:{missingProperty: "capabilities"},message:"must have required property '"+"capabilities"+"'"};
if(vErrors === null){
vErrors = [err35];
}
else {
vErrors.push(err35);
}
errors++;
}
for(const key3 in data15){
if(!((((key3 === "slots") || (key3 === "navigate")) || (key3 === "notify")) || (key3 === "capabilities"))){
const err36 = {instancePath:instancePath+"/bridge",schemaPath:"#/properties/bridge/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key3},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err36];
}
else {
vErrors.push(err36);
}
errors++;
}
}
if(data15.slots !== undefined){
let data16 = data15.slots;
if(Array.isArray(data16)){
const len5 = data16.length;
for(let i6=0; i6<len5; i6++){
let data17 = data16[i6];
if(!((((((data17 === "qian.shell.nav/v1") || (data17 === "qian.shell.settings/v1")) || (data17 === "qian.shell.user-menu/v1")) || (data17 === "qian.shell.command/v1")) || (data17 === "qian.shell.preview/v1")) || (data17 === "qian.shell.client-action/v1"))){
const err37 = {instancePath:instancePath+"/bridge/slots/" + i6,schemaPath:"#/$defs/slotId/enum",keyword:"enum",params:{allowedValues: schema47.enum},message:"must be equal to one of the allowed values"};
if(vErrors === null){
vErrors = [err37];
}
else {
vErrors.push(err37);
}
errors++;
}
}
let i7 = data16.length;
let j1;
if(i7 > 1){
outer1:
for(;i7--;){
for(j1 = i7; j1--;){
if(func0(data16[i7], data16[j1])){
const err38 = {instancePath:instancePath+"/bridge/slots",schemaPath:"#/properties/bridge/properties/slots/uniqueItems",keyword:"uniqueItems",params:{i: i7, j: j1},message:"must NOT have duplicate items (items ## "+j1+" and "+i7+" are identical)"};
if(vErrors === null){
vErrors = [err38];
}
else {
vErrors.push(err38);
}
errors++;
break outer1;
}
}
}
}
}
else {
const err39 = {instancePath:instancePath+"/bridge/slots",schemaPath:"#/properties/bridge/properties/slots/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err39];
}
else {
vErrors.push(err39);
}
errors++;
}
}
if(data15.navigate !== undefined){
let data18 = data15.navigate;
if(Array.isArray(data18)){
const len6 = data18.length;
for(let i8=0; i8<len6; i8++){
let data19 = data18[i8];
if(typeof data19 === "string"){
if(func1(data19) > 256){
const err40 = {instancePath:instancePath+"/bridge/navigate/" + i8,schemaPath:"#/$defs/route/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err40];
}
else {
vErrors.push(err40);
}
errors++;
}
if(func1(data19) < 1){
const err41 = {instancePath:instancePath+"/bridge/navigate/" + i8,schemaPath:"#/$defs/route/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err41];
}
else {
vErrors.push(err41);
}
errors++;
}
if(!pattern5.test(data19)){
const err42 = {instancePath:instancePath+"/bridge/navigate/" + i8,schemaPath:"#/$defs/route/pattern",keyword:"pattern",params:{pattern: "^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"},message:"must match pattern \""+"^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"+"\""};
if(vErrors === null){
vErrors = [err42];
}
else {
vErrors.push(err42);
}
errors++;
}
}
else {
const err43 = {instancePath:instancePath+"/bridge/navigate/" + i8,schemaPath:"#/$defs/route/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err43];
}
else {
vErrors.push(err43);
}
errors++;
}
}
let i9 = data18.length;
let j2;
if(i9 > 1){
outer2:
for(;i9--;){
for(j2 = i9; j2--;){
if(func0(data18[i9], data18[j2])){
const err44 = {instancePath:instancePath+"/bridge/navigate",schemaPath:"#/properties/bridge/properties/navigate/uniqueItems",keyword:"uniqueItems",params:{i: i9, j: j2},message:"must NOT have duplicate items (items ## "+j2+" and "+i9+" are identical)"};
if(vErrors === null){
vErrors = [err44];
}
else {
vErrors.push(err44);
}
errors++;
break outer2;
}
}
}
}
}
else {
const err45 = {instancePath:instancePath+"/bridge/navigate",schemaPath:"#/properties/bridge/properties/navigate/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err45];
}
else {
vErrors.push(err45);
}
errors++;
}
}
if(data15.notify !== undefined){
if(typeof data15.notify !== "boolean"){
const err46 = {instancePath:instancePath+"/bridge/notify",schemaPath:"#/properties/bridge/properties/notify/type",keyword:"type",params:{type: "boolean"},message:"must be boolean"};
if(vErrors === null){
vErrors = [err46];
}
else {
vErrors.push(err46);
}
errors++;
}
}
if(data15.capabilities !== undefined){
let data21 = data15.capabilities;
if(Array.isArray(data21)){
const len7 = data21.length;
for(let i10=0; i10<len7; i10++){
if(!(validate29(data21[i10], {instancePath:instancePath+"/bridge/capabilities/" + i10,parentData:data21,parentDataProperty:i10,rootData,dynamicAnchors}))){
vErrors = vErrors === null ? validate29.errors : vErrors.concat(validate29.errors);
errors = vErrors.length;
}
}
}
else {
const err47 = {instancePath:instancePath+"/bridge/capabilities",schemaPath:"#/properties/bridge/properties/capabilities/type",keyword:"type",params:{type: "array"},message:"must be array"};
if(vErrors === null){
vErrors = [err47];
}
else {
vErrors.push(err47);
}
errors++;
}
}
}
else {
const err48 = {instancePath:instancePath+"/bridge",schemaPath:"#/properties/bridge/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err48];
}
else {
vErrors.push(err48);
}
errors++;
}
}
}
else {
const err49 = {instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err49];
}
else {
vErrors.push(err49);
}
errors++;
}
validate20.errors = vErrors;
return errors === 0;
}
validate20.evaluated = {"props":true,"dynamicProps":false,"dynamicItems":false};

export const validateContribution = validate31;

function validate31(data, {instancePath="", parentData, parentDataProperty, rootData=data, dynamicAnchors={}}={}){
let vErrors = null;
let errors = 0;
const evaluated0 = validate31.evaluated;
if(evaluated0.dynamicProps){
evaluated0.props = undefined;
}
if(evaluated0.dynamicItems){
evaluated0.items = undefined;
}
const _errs2 = errors;
let valid1 = true;
const _errs3 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
let missing0;
if((data.slot === undefined) && (missing0 = "slot")){
const err0 = {};
if(vErrors === null){
vErrors = [err0];
}
else {
vErrors.push(err0);
}
errors++;
}
else {
if(data.slot !== undefined){
if("qian.shell.nav/v1" !== data.slot){
const err1 = {};
if(vErrors === null){
vErrors = [err1];
}
else {
vErrors.push(err1);
}
errors++;
}
}
}
}
var _valid0 = _errs3 === errors;
errors = _errs2;
if(vErrors !== null){
if(_errs2){
vErrors.length = _errs2;
}
else {
vErrors = null;
}
}
if(_valid0){
const _errs5 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.route === undefined){
const err2 = {instancePath,schemaPath:"#/allOf/0/then/required",keyword:"required",params:{missingProperty: "route"},message:"must have required property '"+"route"+"'"};
if(vErrors === null){
vErrors = [err2];
}
else {
vErrors.push(err2);
}
errors++;
}
}
var _valid0 = _errs5 === errors;
valid1 = _valid0;
if(valid1){
var props0 = {};
props0.route = true;
props0.slot = true;
}
}
if(!valid1){
const err3 = {instancePath,schemaPath:"#/allOf/0/if",keyword:"if",params:{failingKeyword: "then"},message:"must match \"then\" schema"};
if(vErrors === null){
vErrors = [err3];
}
else {
vErrors.push(err3);
}
errors++;
}
const _errs7 = errors;
let valid3 = true;
const _errs8 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
let missing1;
if((data.slot === undefined) && (missing1 = "slot")){
const err4 = {};
if(vErrors === null){
vErrors = [err4];
}
else {
vErrors.push(err4);
}
errors++;
}
else {
if(data.slot !== undefined){
if("qian.shell.client-action/v1" !== data.slot){
const err5 = {};
if(vErrors === null){
vErrors = [err5];
}
else {
vErrors.push(err5);
}
errors++;
}
}
}
}
var _valid1 = _errs8 === errors;
errors = _errs7;
if(vErrors !== null){
if(_errs7){
vErrors.length = _errs7;
}
else {
vErrors = null;
}
}
if(_valid1){
const _errs10 = errors;
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.parameters === undefined){
const err6 = {instancePath,schemaPath:"#/allOf/1/then/required",keyword:"required",params:{missingProperty: "parameters"},message:"must have required property '"+"parameters"+"'"};
if(vErrors === null){
vErrors = [err6];
}
else {
vErrors.push(err6);
}
errors++;
}
}
var _valid1 = _errs10 === errors;
valid3 = _valid1;
if(valid3){
var props1 = {};
props1.parameters = true;
props1.slot = true;
}
}
if(!valid3){
const err7 = {instancePath,schemaPath:"#/allOf/1/if",keyword:"if",params:{failingKeyword: "then"},message:"must match \"then\" schema"};
if(vErrors === null){
vErrors = [err7];
}
else {
vErrors.push(err7);
}
errors++;
}
if(props0 !== true && props1 !== undefined){
if(props1 === true){
props0 = true;
}
else {
props0 = props0 || {};
Object.assign(props0, props1);
}
}
if(data && typeof data == "object" && !Array.isArray(data)){
if(data.slot === undefined){
const err8 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "slot"},message:"must have required property '"+"slot"+"'"};
if(vErrors === null){
vErrors = [err8];
}
else {
vErrors.push(err8);
}
errors++;
}
if(data.id === undefined){
const err9 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "id"},message:"must have required property '"+"id"+"'"};
if(vErrors === null){
vErrors = [err9];
}
else {
vErrors.push(err9);
}
errors++;
}
if(data.title === undefined){
const err10 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "title"},message:"must have required property '"+"title"+"'"};
if(vErrors === null){
vErrors = [err10];
}
else {
vErrors.push(err10);
}
errors++;
}
if(data.description === undefined){
const err11 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "description"},message:"must have required property '"+"description"+"'"};
if(vErrors === null){
vErrors = [err11];
}
else {
vErrors.push(err11);
}
errors++;
}
if(data.permission === undefined){
const err12 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "permission"},message:"must have required property '"+"permission"+"'"};
if(vErrors === null){
vErrors = [err12];
}
else {
vErrors.push(err12);
}
errors++;
}
if(data.expose === undefined){
const err13 = {instancePath,schemaPath:"#/required",keyword:"required",params:{missingProperty: "expose"},message:"must have required property '"+"expose"+"'"};
if(vErrors === null){
vErrors = [err13];
}
else {
vErrors.push(err13);
}
errors++;
}
for(const key0 in data){
if(!((((((((key0 === "slot") || (key0 === "id")) || (key0 === "title")) || (key0 === "description")) || (key0 === "permission")) || (key0 === "expose")) || (key0 === "route")) || (key0 === "parameters"))){
const err14 = {instancePath,schemaPath:"#/additionalProperties",keyword:"additionalProperties",params:{additionalProperty: key0},message:"must NOT have additional properties"};
if(vErrors === null){
vErrors = [err14];
}
else {
vErrors.push(err14);
}
errors++;
}
}
if(data.slot !== undefined){
let data2 = data.slot;
if(!((((((data2 === "qian.shell.nav/v1") || (data2 === "qian.shell.settings/v1")) || (data2 === "qian.shell.user-menu/v1")) || (data2 === "qian.shell.command/v1")) || (data2 === "qian.shell.preview/v1")) || (data2 === "qian.shell.client-action/v1"))){
const err15 = {instancePath:instancePath+"/slot",schemaPath:"#/$defs/slotId/enum",keyword:"enum",params:{allowedValues: schema47.enum},message:"must be equal to one of the allowed values"};
if(vErrors === null){
vErrors = [err15];
}
else {
vErrors.push(err15);
}
errors++;
}
}
if(data.id !== undefined){
let data3 = data.id;
if(typeof data3 === "string"){
if(func1(data3) > 256){
const err16 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err16];
}
else {
vErrors.push(err16);
}
errors++;
}
if(func1(data3) < 1){
const err17 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err17];
}
else {
vErrors.push(err17);
}
errors++;
}
if(!pattern9.test(data3)){
const err18 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/pattern",keyword:"pattern",params:{pattern: "^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$"},message:"must match pattern \""+"^(?:[A-Za-z0-9][A-Za-z0-9._/-]*|/(?!/)[A-Za-z0-9._/-]*)$"+"\""};
if(vErrors === null){
vErrors = [err18];
}
else {
vErrors.push(err18);
}
errors++;
}
}
else {
const err19 = {instancePath:instancePath+"/id",schemaPath:"#/$defs/id/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err19];
}
else {
vErrors.push(err19);
}
errors++;
}
}
if(data.title !== undefined){
let data4 = data.title;
if(typeof data4 === "string"){
if(func1(data4) > 256){
const err20 = {instancePath:instancePath+"/title",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err20];
}
else {
vErrors.push(err20);
}
errors++;
}
if(func1(data4) < 1){
const err21 = {instancePath:instancePath+"/title",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err21];
}
else {
vErrors.push(err21);
}
errors++;
}
}
else {
const err22 = {instancePath:instancePath+"/title",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err22];
}
else {
vErrors.push(err22);
}
errors++;
}
}
if(data.description !== undefined){
let data5 = data.description;
if(typeof data5 === "string"){
if(func1(data5) > 256){
const err23 = {instancePath:instancePath+"/description",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err23];
}
else {
vErrors.push(err23);
}
errors++;
}
if(func1(data5) < 1){
const err24 = {instancePath:instancePath+"/description",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err24];
}
else {
vErrors.push(err24);
}
errors++;
}
}
else {
const err25 = {instancePath:instancePath+"/description",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err25];
}
else {
vErrors.push(err25);
}
errors++;
}
}
if(data.permission !== undefined){
let data6 = data.permission;
if(typeof data6 === "string"){
if(func1(data6) > 256){
const err26 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err26];
}
else {
vErrors.push(err26);
}
errors++;
}
if(func1(data6) < 1){
const err27 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err27];
}
else {
vErrors.push(err27);
}
errors++;
}
}
else {
const err28 = {instancePath:instancePath+"/permission",schemaPath:"#/$defs/text/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err28];
}
else {
vErrors.push(err28);
}
errors++;
}
}
if(data.expose !== undefined){
let data7 = data.expose;
if(typeof data7 === "string"){
if(func1(data7) > 256){
const err29 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err29];
}
else {
vErrors.push(err29);
}
errors++;
}
if(func1(data7) < 1){
const err30 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err30];
}
else {
vErrors.push(err30);
}
errors++;
}
if(!pattern4.test(data7)){
const err31 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/pattern",keyword:"pattern",params:{pattern: "^[A-Za-z][A-Za-z0-9_-]*$"},message:"must match pattern \""+"^[A-Za-z][A-Za-z0-9_-]*$"+"\""};
if(vErrors === null){
vErrors = [err31];
}
else {
vErrors.push(err31);
}
errors++;
}
}
else {
const err32 = {instancePath:instancePath+"/expose",schemaPath:"#/$defs/expose/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err32];
}
else {
vErrors.push(err32);
}
errors++;
}
}
if(data.route !== undefined){
let data8 = data.route;
if(typeof data8 === "string"){
if(func1(data8) > 256){
const err33 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/maxLength",keyword:"maxLength",params:{limit: 256},message:"must NOT have more than 256 characters"};
if(vErrors === null){
vErrors = [err33];
}
else {
vErrors.push(err33);
}
errors++;
}
if(func1(data8) < 1){
const err34 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/minLength",keyword:"minLength",params:{limit: 1},message:"must NOT have fewer than 1 characters"};
if(vErrors === null){
vErrors = [err34];
}
else {
vErrors.push(err34);
}
errors++;
}
if(!pattern5.test(data8)){
const err35 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/pattern",keyword:"pattern",params:{pattern: "^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"},message:"must match pattern \""+"^/(?!/)(?!\\.{1,2}(?:/|$))(?!.*\\/\\.{1,2}(?:/|$))[^\\\\?#\\u0000-\\u0020]*$"+"\""};
if(vErrors === null){
vErrors = [err35];
}
else {
vErrors.push(err35);
}
errors++;
}
}
else {
const err36 = {instancePath:instancePath+"/route",schemaPath:"#/$defs/route/type",keyword:"type",params:{type: "string"},message:"must be string"};
if(vErrors === null){
vErrors = [err36];
}
else {
vErrors.push(err36);
}
errors++;
}
}
if(data.parameters !== undefined){
let data9 = data.parameters;
if(!(data9 && typeof data9 == "object" && !Array.isArray(data9))){
const err37 = {instancePath:instancePath+"/parameters",schemaPath:"#/properties/parameters/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err37];
}
else {
vErrors.push(err37);
}
errors++;
}
}
}
else {
const err38 = {instancePath,schemaPath:"#/type",keyword:"type",params:{type: "object"},message:"must be object"};
if(vErrors === null){
vErrors = [err38];
}
else {
vErrors.push(err38);
}
errors++;
}
validate31.errors = vErrors;
return errors === 0;
}
validate31.evaluated = {"props":true,"dynamicProps":false,"dynamicItems":false};

