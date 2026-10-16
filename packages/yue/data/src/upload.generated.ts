// 由 OpenAPI 契约生成；运行对应包的 contracts:generate。
export interface paths {
    "/uploads:lookup": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query: {
                    sha256: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description 已有文件 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["FileObject"];
                    };
                };
                /** @description ErrorDetail */
                404: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/problem+json": components["schemas"]["error-detail.schema"];
                    };
                };
            };
        };
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/uploads:init": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": {
                        name: string;
                        size: number;
                        contentType: string;
                        sha256: string;
                    };
                };
            };
            responses: {
                /** @description 可重入分片会话 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["UploadSession"];
                    };
                };
                /** @description ErrorDetail */
                default: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/problem+json": components["schemas"]["error-detail.schema"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/uploads/{id}/parts/{n}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                    n: number;
                };
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/octet-stream": string;
                };
            };
            responses: {
                /** @description 分片已接收 */
                204: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content?: never;
                };
                /** @description ErrorDetail */
                default: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/problem+json": components["schemas"]["error-detail.schema"];
                    };
                };
            };
        };
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/uploads/{id}:complete": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody: {
                content: {
                    "application/json": {
                        sha256: string;
                        partCount: number;
                    };
                };
            };
            responses: {
                /** @description 完整文件 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["FileObject"];
                    };
                };
                /** @description ErrorDetail */
                default: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/problem+json": components["schemas"]["error-detail.schema"];
                    };
                };
            };
        };
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        FileObject: {
            id: string;
            name: string;
            size: number;
            contentType: string;
        };
        UploadSession: {
            uploadId: string;
            partSize: number;
            uploadedParts: number[];
        };
        /** @description 稳定错误码：大写下划线，全局唯一，登记在错误码目录（error-catalog.schema.json）。 */
        Code: string;
        /** @description RFC 6901 JSON Pointer 的 URI 片段形式，可带文档前缀：请求输入写 #/items/0/qty；文件写 <相对路径>#/<pointer>，如 .qian-plugin/plugin.json#/runtime/nuget/0。 */
        Pointer: string;
        /** @description 允许的取值（枚举值或可选项），任意 JSON 值。 */
        Allowed: unknown[];
        /** @description W3C Trace Context 的 trace-id（32 位小写十六进制），与日志、审计、事件信封 trace_parent 中的 trace-id 一致。 */
        TraceId: string;
        FieldError: {
            code: components["schemas"]["Code"];
            message: string;
            pointer: components["schemas"]["Pointer"];
            allowed?: components["schemas"]["Allowed"];
        };
        /**
         * ErrorDetail
         * @description Qian 的错误响应体：RFC 9457 problem+json 的 Qian 配置文件。媒体类型 application/problem+json。type 区分协议错误与业务错误；code 为错误码目录中登记的稳定错误码；字段错误给出 JSON Pointer。规则见 contracts/v0/README.md「ErrorDetail」。
         */
        "error-detail.schema": {
            /** @description 问题类型 URI：https://qian.dev/problems/<kind>/<code>；kind 为 protocol 或 business，末段与 code 相同。它是标识符，不要求可解引用。 */
            type: string;
            /** @description 问题类型的简短摘要（按请求语言本地化），同一 code 不随出现而变。 */
            title: string;
            /** @description HTTP 状态码，与响应状态行一致。业务错误只用 409 / 422，协议错误不用这两个值。 */
            status: number;
            /** @description RFC 9457 通用客户端读取的说明；出现时与 message 相同。 */
            detail?: string;
            /** @description 本次出错的请求路径（URI 引用）。 */
            instance?: string;
            code: components["schemas"]["Code"];
            /** @description 面向人与模型的说明（按请求语言本地化），由错误码目录的文案模板渲染。 */
            message: string;
            trace_id: components["schemas"]["TraceId"];
            /** @description 出错的输入位置。errors 非空时取 errors[0].pointer。 */
            pointer?: components["schemas"]["Pointer"];
            /** @description 该位置允许的取值。errors 非空时取 errors[0].allowed。 */
            allowed?: components["schemas"]["Allowed"];
            /** @description 逐项的字段错误（输入校验失败时必有）。 */
            errors?: components["schemas"]["FieldError"][];
            $defs: {
                /** @description 稳定错误码：大写下划线，全局唯一，登记在错误码目录（error-catalog.schema.json）。 */
                Code: string;
                /** @description W3C Trace Context 的 trace-id（32 位小写十六进制），与日志、审计、事件信封 trace_parent 中的 trace-id 一致。 */
                TraceId: string;
                /** @description RFC 6901 JSON Pointer 的 URI 片段形式，可带文档前缀：请求输入写 #/items/0/qty；文件写 <相对路径>#/<pointer>，如 .qian-plugin/plugin.json#/runtime/nuget/0。 */
                Pointer: string;
                /** @description 允许的取值（枚举值或可选项），任意 JSON 值。 */
                Allowed: unknown[];
                FieldError: {
                    code: components["schemas"]["Code"];
                    message: string;
                    pointer: components["schemas"]["Pointer"];
                    allowed?: components["schemas"]["Allowed"];
                };
            };
        } & (unknown & unknown);
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
