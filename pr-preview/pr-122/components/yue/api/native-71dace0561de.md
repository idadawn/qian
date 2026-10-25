> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 通用 DOM 属性

以下组件共享完全相同的继承属性与实例类型：`Input`。

| 属性 | 类型 | 必填 | 默认值 | 说明 |
| ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- | --- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| form | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| slot | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| title | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| pattern | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| key | `Key \| null \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| formAction | `string \| ((formData: FormData) => void \| Promise<void>) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| formEncType | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| formMethod | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| formNoValidate | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| formTarget | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| name | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| type | `HTMLInputTypeAttribute \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| suppressContentEditableWarning | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| suppressHydrationWarning | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| accessKey | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| autoCapitalize | `"none" \| "off" \| "on" \| "sentences" \| "words" \| "characters" \| (string & {}) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| autoFocus | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| contentEditable | `Booleanish \| "inherit" \| "plaintext-only" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| contextMenu | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| dir | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| draggable | `Booleanish \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| enterKeyHint | `"search" \| "enter" \| "done" \| "go" \| "next" \| "previous" \| "send" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| hidden | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| id | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| lang | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| nonce | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| spellCheck | `Booleanish \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| tabIndex | `number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| translate | `"yes" \| "no" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| radioGroup | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| role | `AriaRole \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| about | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| content | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| datatype | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| inlist | `any` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| prefix | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| property | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| rel | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| resource | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| rev | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| typeof | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| vocab | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| autoCorrect | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| autoSave | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| itemProp | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| itemScope | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| itemType | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| itemID | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| itemRef | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| results | `number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| security | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| unselectable | `"off" \| "on" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| popover | `"" \| "auto" \| "manual" \| "hint" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| popoverTargetAction | `"toggle" \| "show" \| "hide" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| popoverTarget | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| inert | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| inputMode | `"none" \| "search" \| "text" \| "tel" \| "url" \| "email" \| "numeric" \| "decimal" \| undefined` | 否 | 未指定 | Hints at the type of data that might be entered by the user while editing the element or its contents |
| is | `string \| undefined` | 否 | 未指定 | Specify that a standard HTML element should behave like a defined custom built-in element |
| exportparts | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
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
| onCopy | `((event: import("@base-ui/react").BaseUIEvent<ClipboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCopyCapture | `((event: import("@base-ui/react").BaseUIEvent<ClipboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCut | `((event: import("@base-ui/react").BaseUIEvent<ClipboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCutCapture | `((event: import("@base-ui/react").BaseUIEvent<ClipboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPaste | `((event: import("@base-ui/react").BaseUIEvent<ClipboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPasteCapture | `((event: import("@base-ui/react").BaseUIEvent<ClipboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionEnd | `((event: import("@base-ui/react").BaseUIEvent<CompositionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionEndCapture | `((event: import("@base-ui/react").BaseUIEvent<CompositionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionStart | `((event: import("@base-ui/react").BaseUIEvent<CompositionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionStartCapture | `((event: import("@base-ui/react").BaseUIEvent<CompositionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionUpdate | `((event: import("@base-ui/react").BaseUIEvent<CompositionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCompositionUpdateCapture | `((event: import("@base-ui/react").BaseUIEvent<CompositionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onFocus | `((event: import("@base-ui/react").BaseUIEvent<FocusEvent<HTMLInputElement, Element>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onFocusCapture | `((event: import("@base-ui/react").BaseUIEvent<FocusEvent<HTMLInputElement, Element>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBlur | `((event: import("@base-ui/react").BaseUIEvent<FocusEvent<HTMLInputElement, Element>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBlurCapture | `((event: import("@base-ui/react").BaseUIEvent<FocusEvent<HTMLInputElement, Element>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onChange | `((event: import("@base-ui/react").BaseUIEvent<ChangeEvent<HTMLInputElement, HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onChangeCapture | `((event: import("@base-ui/react").BaseUIEvent<ChangeEvent<HTMLInputElement, Element>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBeforeInput | `((event: import("@base-ui/react").BaseUIEvent<InputEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBeforeInputCapture | `((event: import("@base-ui/react").BaseUIEvent<InputEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInput | `((event: import("@base-ui/react").BaseUIEvent<InputEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInputCapture | `((event: import("@base-ui/react").BaseUIEvent<InputEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onReset | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onResetCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSubmit | `((event: import("@base-ui/react").BaseUIEvent<SubmitEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSubmitCapture | `((event: import("@base-ui/react").BaseUIEvent<SubmitEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInvalid | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onInvalidCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoad | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onError | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onErrorCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyDown | `((event: import("@base-ui/react").BaseUIEvent<KeyboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyDownCapture | `((event: import("@base-ui/react").BaseUIEvent<KeyboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyPress | `((event: import("@base-ui/react").BaseUIEvent<KeyboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyPressCapture | `((event: import("@base-ui/react").BaseUIEvent<KeyboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyUp | `((event: import("@base-ui/react").BaseUIEvent<KeyboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onKeyUpCapture | `((event: import("@base-ui/react").BaseUIEvent<KeyboardEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAbort | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAbortCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlay | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlayCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlayThrough | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onCanPlayThroughCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDurationChange | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDurationChangeCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEmptied | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEmptiedCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEncrypted | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEncryptedCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEnded | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onEndedCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedData | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedDataCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedMetadata | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadedMetadataCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadStart | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLoadStartCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPause | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPauseCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlay | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlayCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlaying | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPlayingCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onProgress | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onProgressCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onRateChange | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onRateChangeCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeeked | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeekedCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeeking | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSeekingCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onStalled | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onStalledCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSuspend | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSuspendCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTimeUpdate | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTimeUpdateCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onVolumeChange | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onVolumeChangeCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWaiting | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWaitingCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAuxClick | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAuxClickCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClick | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onClickCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onContextMenu | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onContextMenuCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDoubleClick | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDoubleClickCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDrag | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEnd | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEndCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEnter | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragEnterCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragExit | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragExitCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragLeave | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragLeaveCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragOver | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragOverCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragStart | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDragStartCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDrop | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onDropCapture | `((event: import("@base-ui/react").BaseUIEvent<DragEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseDown | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseDownCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseEnter | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseLeave | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseMove | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseMoveCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOut | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOutCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOver | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseOverCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseUp | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onMouseUpCapture | `((event: import("@base-ui/react").BaseUIEvent<MouseEvent<HTMLInputElement, globalThis.MouseEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSelect | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onSelectCapture | `((event: import("@base-ui/react").BaseUIEvent<SyntheticEvent<HTMLInputElement, Event>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchCancel | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchCancelCapture | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchEnd | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchEndCapture | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchMove | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchMoveCapture | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchStart | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTouchStartCapture | `((event: import("@base-ui/react").BaseUIEvent<TouchEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerDown | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerDownCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerMove | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerMoveCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerUp | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerUpCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerCancel | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerCancelCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerEnter | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerLeave | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOver | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOverCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOut | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onPointerOutCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onGotPointerCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onGotPointerCaptureCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLostPointerCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onLostPointerCaptureCapture | `((event: import("@base-ui/react").BaseUIEvent<PointerEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScroll | `((event: import("@base-ui/react").BaseUIEvent<UIEvent<HTMLInputElement, globalThis.UIEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScrollCapture | `((event: import("@base-ui/react").BaseUIEvent<UIEvent<HTMLInputElement, globalThis.UIEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScrollEnd | `((event: import("@base-ui/react").BaseUIEvent<UIEvent<HTMLInputElement, globalThis.UIEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onScrollEndCapture | `((event: import("@base-ui/react").BaseUIEvent<UIEvent<HTMLInputElement, globalThis.UIEvent>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWheel | `((event: import("@base-ui/react").BaseUIEvent<WheelEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onWheelCapture | `((event: import("@base-ui/react").BaseUIEvent<WheelEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationStart | `((event: import("@base-ui/react").BaseUIEvent<AnimationEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationStartCapture | `((event: import("@base-ui/react").BaseUIEvent<AnimationEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationEnd | `((event: import("@base-ui/react").BaseUIEvent<AnimationEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationEndCapture | `((event: import("@base-ui/react").BaseUIEvent<AnimationEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationIteration | `((event: import("@base-ui/react").BaseUIEvent<AnimationEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onAnimationIterationCapture | `((event: import("@base-ui/react").BaseUIEvent<AnimationEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onToggle | `((event: import("@base-ui/react").BaseUIEvent<ToggleEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onBeforeToggle | `((event: import("@base-ui/react").BaseUIEvent<ToggleEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionCancel | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionCancelCapture | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionEnd | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionEndCapture | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionRun | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionRunCapture | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionStart | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| onTransitionStartCapture | `((event: import("@base-ui/react").BaseUIEvent<TransitionEvent<HTMLInputElement>>) => void) \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| list | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| step | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| autoComplete | `HTMLInputAutoCompleteAttribute \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| required | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| readOnly | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| accept | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| alt | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| capture | `boolean \| "user" \| "environment" \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| checked | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| height | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| max | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| maxLength | `number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| min | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| minLength | `number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| multiple | `boolean \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| placeholder | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| size | `number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| src | `string \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
| width | `string \| number \| undefined` | 否 | 未指定 | 以 TypeScript 类型约束为准。 |
