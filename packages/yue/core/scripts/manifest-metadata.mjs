import ts from 'typescript';

// 每次沿真实属性符号下钻；不把数组元素或组合分支冒充成一个 TS 属性。
export function updatePropsMetadata(schema, type, checker, component) {
  const expand = types => types.flatMap(t => t.isUnion() ? expand(t.types) : [t])
    .filter(t => !(t.flags & (ts.TypeFlags.Null | ts.TypeFlags.Undefined | ts.TypeFlags.Never)));
  const at = symbol => symbol.valueDeclaration ?? symbol.declarations?.[0];
  const typeOf = symbol => checker.getTypeOfSymbolAtLocation(symbol, at(symbol));
  const array = type => checker.isArrayType(type) || checker.isTupleType(type);
  function matches(type, node) {
    if (type.flags & (ts.TypeFlags.Any | ts.TypeFlags.Unknown)) return true;
    const kinds = node.type ? [node.type].flat() : node.properties ? ['object'] : node.items || node.prefixItems ? ['array'] : [];
    if (kinds.length && !kinds.some(kind => ({
      string: !!(type.flags & ts.TypeFlags.StringLike),
      number: !!(type.flags & ts.TypeFlags.NumberLike),
      integer: !!(type.flags & ts.TypeFlags.NumberLike),
      boolean: !!(type.flags & ts.TypeFlags.BooleanLike),
      array: array(type),
      object: !array(type) && !!(type.flags & (ts.TypeFlags.Object | ts.TypeFlags.Intersection)),
    })[kind])) return false;
    const values = 'const' in node ? [node.const] : node.enum;
    if (values && type.isLiteral() && !values.includes(type.value)) return false;
    if (values && (type.flags & ts.TypeFlags.BooleanLiteral) && !values.includes(checker.typeToString(type) === 'true')) return false;
    // 用鉴别字段选择联合类型分支，避免把另一分支的同名属性注释套过来。
    for (const [name, child] of Object.entries(node.properties ?? {})) {
      if (!child || typeof child !== 'object' || (!('const' in child) && !child.enum)) continue;
      const prop = checker.getPropertyOfType(type, name);
      if (prop && !expand([typeOf(prop)]).some(t => matches(t, child))) return false;
    }
    return true;
  }
  function visit(node, types, location, property) {
    if (!node || typeof node !== 'object') return;
    const candidates = expand(types).filter(t => matches(t, node));
    if (property) {
      node.title = property.name;
      const comments = [...new Set(property.symbols.map(symbol => ts.displayPartsToString(symbol.getDocumentationComment(checker)).trim()).filter(Boolean))];
      if (comments.length > 1) throw new Error(`${location}: 联合分支有不同注释，请用鉴别字段明确分支`);
      if (comments[0]) node.description = comments[0];
      else delete node.description;
    } else {
      // items / 组合分支没有独立属性符号，清掉遗留或复制来的元数据。
      delete node.title;
      delete node.description;
    }
    for (const [name, child] of Object.entries(node.properties ?? {})) {
      const symbols = candidates.map(t => checker.getPropertyOfType(t, name)).filter(Boolean);
      if (!symbols.length) throw new Error(`${location}.${name}: TypeScript 类型中不存在该属性`);
      visit(child, symbols.map(typeOf), `${location}.${name}`, { name, symbols });
    }
    function elementTypes(index) {
      return candidates.filter(array).flatMap(t => {
        const args = checker.getTypeArguments(t);
        if (checker.isTupleType(t)) return index === undefined ? args : args.slice(index, index + 1);
        return args.slice(0, 1);
      });
    }
    function item(child, index, locationSuffix) {
      const elements = elementTypes(index);
      if (!elements.length) throw new Error(`${location}${locationSuffix}: TypeScript 类型中不存在对应数组元素`);
      visit(child, elements, `${location}${locationSuffix}`);
    }
    if (Array.isArray(node.items)) node.items.forEach((child, index) => item(child, index, `[${index}]`));
    else if (node.items && typeof node.items === 'object') item(node.items, undefined, '[]');
    node.prefixItems?.forEach((child, index) => item(child, index, `[${index}]`));
    for (const keyword of ['anyOf', 'oneOf', 'allOf']) {
      node[keyword]?.forEach((child, index) => visit(child, candidates, `${location}.${keyword}[${index}]`));
    }
  }
  visit(schema, [type], component);
  return schema;
}
