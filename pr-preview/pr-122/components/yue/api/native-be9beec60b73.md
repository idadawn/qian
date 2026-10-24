> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 通用 DOM 属性

以下组件共享完全相同的继承属性与实例类型：`Icon`。

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| string | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| color | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| style | `CSSProperties \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| slot | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| clipPath | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| filter | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| mask | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| path | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| key | `Key \| null \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| type | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| suppressHydrationWarning | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| id | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| lang | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nonce | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| tabIndex | `number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| role | `AriaRole \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| part | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| aria-activedescendant | `string \| undefined` | 否 | 未指定 | Identifies the currently active element when DOM focus is on a composite widget, textbox, group, or application. |
| aria-atomic | `Booleanish \| undefined` | 否 | 未指定 | Indicates whether assistive technologies will present all, or only parts of, the changed region based on the change notifications defined by the aria-relevant attribute. |
| aria-autocomplete | `"none" \| "list" \| "inline" \| "both" \| undefined` | 否 | 未指定 | Indicates whether inputting text could trigger display of one or more predictions of the user's intended value for an input and specifies how predictions would be presented if they are made. |
| aria-braillelabel | `string \| undefined` | 否 | 未指定 | Defines a string value that labels the current element, which is intended to be converted into Braille. |
| aria-brailleroledescription | `string \| undefined` | 否 | 未指定 | Defines a human-readable, author-localized abbreviated description for the role of an element, which is intended to be converted into Braille. |
| aria-busy | `Booleanish \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| aria-checked | `boolean \| "true" \| "false" \| "mixed" \| undefined` | 否 | 未指定 | Indicates the current "checked" state of checkboxes, radio buttons, and other widgets. |
| aria-colcount | `number \| undefined` | 否 | 未指定 | Defines the total number of columns in a table, grid, or treegrid. |
| aria-colindex | `number \| undefined` | 否 | 未指定 | Defines an element's column index or position with respect to the total number of columns within a table, grid, or treegrid. |
| aria-colindextext | `string \| undefined` | 否 | 未指定 | Defines a human readable text alternative of aria-colindex. |
| aria-colspan | `number \| undefined` | 否 | 未指定 | Defines the number of columns spanned by a cell or gridcell within a table, grid, or treegrid. |
| aria-controls | `string \| undefined` | 否 | 未指定 | Identifies the element (or elements) whose contents or presence are controlled by the current element. |
| aria-current | `boolean \| "time" \| "true" \| "false" \| "page" \| "step" \| "location" \| "date" \| undefined` | 否 | 未指定 | Indicates the element that represents the current item within a container or set of related elements. |
| aria-describedby | `string \| undefined` | 否 | 未指定 | Identifies the element (or elements) that describes the object. |
| aria-description | `string \| undefined` | 否 | 未指定 | Defines a string value that describes or annotates the current element. |
| aria-details | `string \| undefined` | 否 | 未指定 | Identifies the element that provides a detailed, extended description for the object. |
| aria-disabled | `Booleanish \| undefined` | 否 | 未指定 | Indicates that the element is perceivable but disabled, so it is not editable or otherwise operable. |
| aria-dropeffect | `"none" \| "link" \| "copy" \| "execute" \| "move" \| "popup" \| undefined` | 否 | 未指定 | Indicates what functions can be performed when a dragged object is released on the drop target. |
| aria-errormessage | `string \| undefined` | 否 | 未指定 | Identifies the element that provides an error message for the object. |
| aria-expanded | `Booleanish \| undefined` | 否 | 未指定 | Indicates whether the element, or another grouping element it controls, is currently expanded or collapsed. |
| aria-flowto | `string \| undefined` | 否 | 未指定 | Identifies the next element (or elements) in an alternate reading order of content which, at the user's discretion, allows assistive technology to override the general default of reading in document source order. |
| aria-grabbed | `Booleanish \| undefined` | 否 | 未指定 | Indicates an element's "grabbed" state in a drag-and-drop operation. |
| aria-haspopup | `boolean \| "dialog" \| "menu" \| "true" \| "false" \| "grid" \| "listbox" \| "tree" \| undefined` | 否 | 未指定 | Indicates the availability and type of interactive popup element, such as menu or dialog, that can be triggered by an element. |
| aria-hidden | `Booleanish \| undefined` | 否 | 未指定 | Indicates whether the element is exposed to an accessibility API. |
| aria-invalid | `boolean \| "true" \| "false" \| "grammar" \| "spelling" \| undefined` | 否 | 未指定 | Indicates the entered value does not conform to the format expected by the application. |
| aria-keyshortcuts | `string \| undefined` | 否 | 未指定 | Indicates keyboard shortcuts that an author has implemented to activate or give focus to an element. |
| aria-label | `string \| undefined` | 否 | 未指定 | Defines a string value that labels the current element. |
| aria-labelledby | `string \| undefined` | 否 | 未指定 | Identifies the element (or elements) that labels the current element. |
| aria-level | `number \| undefined` | 否 | 未指定 | Defines the hierarchical level of an element within a structure. |
| aria-live | `"off" \| "assertive" \| "polite" \| undefined` | 否 | 未指定 | Indicates that an element will be updated, and describes the types of updates the user agents, assistive technologies, and user can expect from the live region. |
| aria-modal | `Booleanish \| undefined` | 否 | 未指定 | Indicates whether an element is modal when displayed. |
| aria-multiline | `Booleanish \| undefined` | 否 | 未指定 | Indicates whether a text box accepts multiple lines of input or only a single line. |
| aria-multiselectable | `Booleanish \| undefined` | 否 | 未指定 | Indicates that the user may select more than one item from the current selectable descendants. |
| aria-orientation | `"horizontal" \| "vertical" \| undefined` | 否 | 未指定 | Indicates whether the element's orientation is horizontal, vertical, or unknown/ambiguous. |
| aria-owns | `string \| undefined` | 否 | 未指定 | Identifies an element (or elements) in order to define a visual, functional, or contextual parent/child relationship between DOM elements where the DOM hierarchy cannot be used to represent the relationship. |
| aria-placeholder | `string \| undefined` | 否 | 未指定 | Defines a short hint (a word or short phrase) intended to aid the user with data entry when the control has no value. A hint could be a sample value or a brief description of the expected format. |
| aria-posinset | `number \| undefined` | 否 | 未指定 | Defines an element's number or position in the current set of listitems or treeitems. Not required if all elements in the set are present in the DOM. |
| aria-pressed | `boolean \| "true" \| "false" \| "mixed" \| undefined` | 否 | 未指定 | Indicates the current "pressed" state of toggle buttons. |
| aria-readonly | `Booleanish \| undefined` | 否 | 未指定 | Indicates that the element is not editable, but is otherwise operable. |
| aria-relevant | `"text" \| "additions" \| "additions removals" \| "additions text" \| "all" \| "removals" \| "removals additions" \| "removals text" \| "text additions" \| "text removals" \| undefined` | 否 | 未指定 | Indicates what notifications the user agent will trigger when the accessibility tree within a live region is modified. |
| aria-required | `Booleanish \| undefined` | 否 | 未指定 | Indicates that user input is required on the element before a form may be submitted. |
| aria-roledescription | `string \| undefined` | 否 | 未指定 | Defines a human-readable, author-localized description for the role of an element. |
| aria-rowcount | `number \| undefined` | 否 | 未指定 | Defines the total number of rows in a table, grid, or treegrid. |
| aria-rowindex | `number \| undefined` | 否 | 未指定 | Defines an element's row index or position with respect to the total number of rows within a table, grid, or treegrid. |
| aria-rowindextext | `string \| undefined` | 否 | 未指定 | Defines a human readable text alternative of aria-rowindex. |
| aria-rowspan | `number \| undefined` | 否 | 未指定 | Defines the number of rows spanned by a cell or gridcell within a table, grid, or treegrid. |
| aria-selected | `Booleanish \| undefined` | 否 | 未指定 | Indicates the current "selected" state of various widgets. |
| aria-setsize | `number \| undefined` | 否 | 未指定 | Defines the number of items in the current set of listitems or treeitems. Not required if all elements in the set are present in the DOM. |
| aria-sort | `"none" \| "ascending" \| "descending" \| "other" \| undefined` | 否 | 未指定 | Indicates if items in a table or grid are sorted in ascending or descending order. |
| aria-valuemax | `number \| undefined` | 否 | 未指定 | Defines the maximum allowed value for a range widget. |
| aria-valuemin | `number \| undefined` | 否 | 未指定 | Defines the minimum allowed value for a range widget. |
| aria-valuenow | `number \| undefined` | 否 | 未指定 | Defines the current value for a range widget. |
| aria-valuetext | `string \| undefined` | 否 | 未指定 | Defines the human readable text alternative of aria-valuenow for a range widget. |
| dangerouslySetInnerHTML | `{ __html: string \| TrustedHTML; } \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCopy | `ClipboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCopyCapture | `ClipboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCut | `ClipboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCutCapture | `ClipboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPaste | `ClipboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPasteCapture | `ClipboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionEnd | `CompositionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionEndCapture | `CompositionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionStart | `CompositionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionStartCapture | `CompositionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionUpdate | `CompositionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionUpdateCapture | `CompositionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onFocus | `FocusEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onFocusCapture | `FocusEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBlur | `FocusEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBlurCapture | `FocusEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onChange | `ChangeEventHandler<SVGSVGElement, Element> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onChangeCapture | `ChangeEventHandler<SVGSVGElement, Element> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBeforeInput | `InputEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBeforeInputCapture | `InputEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInput | `InputEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInputCapture | `InputEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onReset | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onResetCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSubmit | `SubmitEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSubmitCapture | `SubmitEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInvalid | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInvalidCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoad | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onError | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onErrorCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyDown | `KeyboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyDownCapture | `KeyboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyPress | `KeyboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyPressCapture | `KeyboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyUp | `KeyboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyUpCapture | `KeyboardEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAbort | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAbortCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlay | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlayCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlayThrough | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlayThroughCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDurationChange | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDurationChangeCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEmptied | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEmptiedCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEncrypted | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEncryptedCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEnded | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEndedCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedData | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedDataCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedMetadata | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedMetadataCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadStart | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadStartCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPause | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPauseCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlay | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlayCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlaying | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlayingCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onProgress | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onProgressCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onRateChange | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onRateChangeCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeeked | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeekedCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeeking | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeekingCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onStalled | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onStalledCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSuspend | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSuspendCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTimeUpdate | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTimeUpdateCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onVolumeChange | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onVolumeChangeCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWaiting | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWaitingCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAuxClick | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAuxClickCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClick | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClickCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onContextMenu | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onContextMenuCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDoubleClick | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDoubleClickCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDrag | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEnd | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEndCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEnter | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEnterCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragExit | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragExitCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragLeave | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragLeaveCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragOver | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragOverCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragStart | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragStartCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDrop | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDropCapture | `DragEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseDown | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseDownCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseEnter | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseLeave | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseMove | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseMoveCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOut | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOutCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOver | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOverCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseUp | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseUpCapture | `MouseEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSelect | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSelectCapture | `ReactEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchCancel | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchCancelCapture | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchEnd | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchEndCapture | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchMove | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchMoveCapture | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchStart | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchStartCapture | `TouchEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerDown | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerDownCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerMove | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerMoveCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerUp | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerUpCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerCancel | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerCancelCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerEnter | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerLeave | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOver | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOverCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOut | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOutCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onGotPointerCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onGotPointerCaptureCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLostPointerCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLostPointerCaptureCapture | `PointerEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScroll | `UIEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScrollCapture | `UIEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScrollEnd | `UIEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScrollEndCapture | `UIEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWheel | `WheelEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWheelCapture | `WheelEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationStart | `AnimationEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationStartCapture | `AnimationEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationEnd | `AnimationEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationEndCapture | `AnimationEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationIteration | `AnimationEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationIterationCapture | `AnimationEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onToggle | `ToggleEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBeforeToggle | `ToggleEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionCancel | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionCancelCapture | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionEnd | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionEndCapture | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionRun | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionRunCapture | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionStart | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionStartCapture | `TransitionEventHandler<SVGSVGElement> \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| version | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| end | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| orientation | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fill | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| values | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| height | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| max | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| min | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| width | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| x | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| y | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| media | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| method | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| target | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| crossOrigin | `CrossOrigin` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| accentHeight | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| accumulate | `"none" \| "sum" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| additive | `"sum" \| "replace" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| alignmentBaseline | `"inherit" \| "auto" \| "alphabetic" \| "hanging" \| "ideographic" \| "mathematical" \| "baseline" \| "before-edge" \| "text-before-edge" \| "middle" \| "central" \| "after-edge" \| "text-after-edge" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| allowReorder | `"yes" \| "no" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| alphabetic | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| amplitude | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| arabicForm | `"initial" \| "medial" \| "terminal" \| "isolated" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| ascent | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| attributeName | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| attributeType | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| autoReverse | `Booleanish \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| azimuth | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| baseFrequency | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| baselineShift | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| baseProfile | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| bbox | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| begin | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| bias | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| by | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| calcMode | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| capHeight | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| clip | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| clipPathUnits | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| clipRule | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| colorInterpolation | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| colorInterpolationFilters | `"inherit" \| "auto" \| "sRGB" \| "linearRGB" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| colorProfile | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| colorRendering | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| contentScriptType | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| contentStyleType | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| cursor | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| cx | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| cy | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| d | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| decelerate | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| descent | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| diffuseConstant | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| direction | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| display | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| divisor | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| dominantBaseline | `"inherit" \| "auto" \| "alphabetic" \| "hanging" \| "ideographic" \| "mathematical" \| "text-before-edge" \| "middle" \| "central" \| "text-after-edge" \| "use-script" \| "no-change" \| "reset-size" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| dur | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| dx | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| dy | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| edgeMode | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| elevation | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| enableBackground | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| exponent | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| externalResourcesRequired | `Booleanish \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fillOpacity | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fillRule | `"inherit" \| "nonzero" \| "evenodd" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| filterRes | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| filterUnits | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| floodColor | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| floodOpacity | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| focusable | `Booleanish \| "auto" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fontFamily | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fontSize | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fontSizeAdjust | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fontStretch | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fontStyle | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fontVariant | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fontWeight | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| format | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fr | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| from | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fx | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| fy | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| g1 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| g2 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| glyphName | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| glyphOrientationHorizontal | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| glyphOrientationVertical | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| glyphRef | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| gradientTransform | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| gradientUnits | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| hanging | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| horizAdvX | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| horizOriginX | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| href | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| ideographic | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| imageRendering | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| in2 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| in | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| intercept | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| k1 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| k2 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| k3 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| k4 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| k | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| kernelMatrix | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| kernelUnitLength | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| kerning | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| keyPoints | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| keySplines | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| keyTimes | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| lengthAdjust | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| letterSpacing | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| lightingColor | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| limitingConeAngle | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| local | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| markerEnd | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| markerHeight | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| markerMid | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| markerStart | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| markerUnits | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| markerWidth | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| maskContentUnits | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| maskUnits | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| mathematical | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| mode | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| numOctaves | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| offset | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| opacity | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| operator | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| order | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| orient | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| origin | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| overflow | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| overlinePosition | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| overlineThickness | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| paintOrder | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| panose1 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| pathLength | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| patternContentUnits | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| patternTransform | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| patternUnits | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| pointerEvents | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| points | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| pointsAtX | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| pointsAtY | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| pointsAtZ | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| preserveAlpha | `Booleanish \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| preserveAspectRatio | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| primitiveUnits | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| r | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| radius | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| refX | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| refY | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| renderingIntent | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| repeatCount | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| repeatDur | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| requiredExtensions | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| requiredFeatures | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| restart | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| result | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| rotate | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| rx | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| ry | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| scale | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| seed | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| shapeRendering | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| slope | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| spacing | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| specularConstant | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| specularExponent | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| speed | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| spreadMethod | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| startOffset | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| stdDeviation | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| stemh | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| stemv | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| stitchTiles | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| stopColor | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| stopOpacity | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strikethroughPosition | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strikethroughThickness | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| stroke | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strokeDasharray | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strokeDashoffset | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strokeLinecap | `"inherit" \| "butt" \| "round" \| "square" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strokeLinejoin | `"inherit" \| "round" \| "miter" \| "bevel" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strokeMiterlimit | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strokeOpacity | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| strokeWidth | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| surfaceScale | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| systemLanguage | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| tableValues | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| targetX | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| targetY | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| textAnchor | `"inherit" \| "start" \| "end" \| "middle" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| textDecoration | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| textLength | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| textRendering | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| to | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| transform | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| u1 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| u2 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| underlinePosition | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| underlineThickness | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| unicode | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| unicodeBidi | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| unicodeRange | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| unitsPerEm | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vAlphabetic | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vectorEffect | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vertAdvY | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vertOriginX | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vertOriginY | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vHanging | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vIdeographic | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| viewBox | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| viewTarget | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| visibility | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vMathematical | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| widths | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| wordSpacing | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| writingMode | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| x1 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| x2 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xChannelSelector | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xHeight | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xlinkActuate | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xlinkArcrole | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xlinkHref | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xlinkRole | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xlinkShow | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xlinkTitle | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xlinkType | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xmlBase | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xmlLang | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xmlns | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xmlnsXlink | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| xmlSpace | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| y1 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| y2 | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| yChannelSelector | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| z | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| zoomAndPan | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
