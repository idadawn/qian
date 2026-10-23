// 由 contracts/v0/error-codes.json 派生；禁止手改。
export const errorCatalog = {
  "CONFLICT": {
    "code": "CONFLICT",
    "kind": "business",
    "status": 409,
    "type": "https://qian.dev/problems/business/CONFLICT",
    "owner": "qian.runtime",
    "retryable": false,
    "title": {
      "en": "Conflict",
      "zh-CN": "状态冲突"
    },
    "message": {
      "en": "The resource state has changed or conflicts with existing data. Read the latest state before deciding whether to retry.",
      "zh-CN": "资源状态已变化或与现有数据冲突。重新读取最新状态后再决定是否重试。"
    }
  },
  "INPUT_INVALID": {
    "code": "INPUT_INVALID",
    "kind": "protocol",
    "status": 400,
    "type": "https://qian.dev/problems/protocol/INPUT_INVALID",
    "owner": "qian.runtime",
    "retryable": false,
    "title": {
      "en": "Input does not match the contract",
      "zh-CN": "输入不符合契约"
    },
    "message": {
      "en": "The input does not match the capability's input schema; see errors for each violation.",
      "zh-CN": "输入不符合该能力的输入 Schema，逐项错误见 errors。"
    }
  },
  "INSUFFICIENT_SCOPE": {
    "code": "INSUFFICIENT_SCOPE",
    "kind": "protocol",
    "status": 403,
    "type": "https://qian.dev/problems/protocol/INSUFFICIENT_SCOPE",
    "owner": "qian.runtime",
    "retryable": false,
    "title": {
      "en": "Insufficient scope",
      "zh-CN": "令牌 scope 不足"
    },
    "message": {
      "en": "The token lacks a scope this operation requires; obtain a token with the scope named in the WWW-Authenticate header and retry.",
      "zh-CN": "令牌缺少该操作所需的 scope，按 WWW-Authenticate 头中的 scope 申请新令牌后重试。"
    }
  },
  "NOT_FOUND": {
    "code": "NOT_FOUND",
    "kind": "protocol",
    "status": 404,
    "type": "https://qian.dev/problems/protocol/NOT_FOUND",
    "owner": "qian.runtime",
    "retryable": false,
    "title": {
      "en": "Not found",
      "zh-CN": "资源不存在"
    },
    "message": {
      "en": "The requested resource does not exist or is not visible to the caller.",
      "zh-CN": "请求的资源不存在或对当前调用者不可见。"
    }
  }
} as const;
