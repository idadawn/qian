// 由 sse-event.schema.json 生成；运行 contracts:generate。

/**
 * S1 SSE 事件草案。只描述事件数据，不携带认证令牌；clientAction 的 name 与 parameters 对应客户端动作注册表。
 */
export type SseEvent =
  | {
      version: "v1";
      id: string;
      type: "text";
      payload: {
        text: string;
      };
    }
  | {
      version: "v1";
      id: string;
      type: "recommend";
      payload: {
        items: {
          title: string;
          value: string;
        }[];
      };
    }
  | {
      version: "v1";
      id: string;
      type: "toolCall";
      payload: {
        callId: string;
        name: string;
        arguments: {
          [k: string]: unknown;
        };
      };
    }
  | {
      version: "v1";
      id: string;
      type: "toolResult";
      payload: {
        callId: string;
        result: unknown;
      };
    }
  | {
      version: "v1";
      id: string;
      type: "artifact";
      payload: {
        id: string;
        title: string;
        uri: string;
      };
    }
  | {
      version: "v1";
      id: string;
      type: "clientAction";
      payload: {
        name: string;
        parameters: {
          [k: string]: unknown;
        };
      };
    }
  | {
      version: "v1";
      id: string;
      type: "progress";
      payload: {
        value: number;
        messageKey: string;
      };
    }
  | {
      version: "v1";
      id: string;
      type: "end";
      payload: {
        reason: string;
      };
    }
  | {
      version: "v1";
      id: string;
      type: "error";
      payload: ErrorDetail;
    };
/**
 * Qian 的错误响应体：RFC 9457 problem+json 的 Qian 配置文件。媒体类型 application/problem+json。type 区分协议错误与业务错误；code 为错误码目录中登记的稳定错误码；字段错误给出 JSON Pointer。规则见 contracts/v0/README.md「ErrorDetail」。
 */
export type ErrorDetail = {
  /**
   * 问题类型 URI：https://qian.dev/problems/<kind>/<code>；kind 为 protocol 或 business，末段与 code 相同。它是标识符，不要求可解引用。
   */
  type: string;
  /**
   * 问题类型的简短摘要（按请求语言本地化），同一 code 不随出现而变。
   */
  title: string;
  /**
   * HTTP 状态码，与响应状态行一致。业务错误只用 409 / 422，协议错误不用这两个值。
   */
  status: number;
  /**
   * RFC 9457 通用客户端读取的说明；出现时与 message 相同。
   */
  detail?: string;
  /**
   * 本次出错的请求路径（URI 引用）。
   */
  instance?: string;
  /**
   * 稳定错误码：大写下划线，全局唯一，登记在错误码目录（error-catalog.schema.json）。
   */
  code: string;
  /**
   * 面向人与模型的说明（按请求语言本地化），由错误码目录的文案模板渲染。
   */
  message: string;
  /**
   * W3C Trace Context 的 trace-id（32 位小写十六进制），与日志、审计、事件信封 trace_parent 中的 trace-id 一致。
   */
  trace_id: string;
  /**
   * 出错的输入位置。errors 非空时取 errors[0].pointer。
   */
  pointer?: string;
  allowed?: Allowed;
  /**
   * 逐项的字段错误（输入校验失败时必有）。
   *
   * @minItems 1
   * @maxItems 100
   */
  errors?: [FieldError, ...FieldError[]];
};
/**
 * 该位置允许的取值。errors 非空时取 errors[0].allowed。
 *
 * @minItems 1
 * @maxItems 100
 */
export type Allowed = [unknown, ...unknown[]];
/**
 * 允许的取值（枚举值或可选项），任意 JSON 值。
 *
 * @minItems 1
 * @maxItems 100
 */
export type Allowed1 = [unknown, ...unknown[]];

export interface FieldError {
  /**
   * 稳定错误码：大写下划线，全局唯一，登记在错误码目录（error-catalog.schema.json）。
   */
  code: string;
  message: string;
  /**
   * RFC 6901 JSON Pointer 的 URI 片段形式，可带文档前缀：请求输入写 #/items/0/qty；文件写 <相对路径>#/<pointer>，如 .qian-plugin/plugin.json#/runtime/nuget/0。
   */
  pointer: string;
  allowed?: Allowed1;
}
