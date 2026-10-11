// 由 contracts/v0/error-codes.json 派生；禁止手改。
export const errorCatalog = {
  "DEPENDENCY_CYCLE": {
    "code": "DEPENDENCY_CYCLE",
    "kind": "protocol",
    "status": 400,
    "type": "https://qian.dev/problems/protocol/DEPENDENCY_CYCLE",
    "owner": "qian.runtime",
    "retryable": false,
    "title": {
      "en": "Dependency cycle",
      "zh-CN": "服务依赖成环"
    },
    "message": {
      "en": "Service dependencies contain a cycle: {cycle}.",
      "zh-CN": "服务依赖存在环路：{cycle}。"
    }
  },
  "DEPENDENCY_MISSING": {
    "code": "DEPENDENCY_MISSING",
    "kind": "protocol",
    "status": 400,
    "type": "https://qian.dev/problems/protocol/DEPENDENCY_MISSING",
    "owner": "qian.runtime",
    "retryable": false,
    "title": {
      "en": "Missing dependency",
      "zh-CN": "缺少服务依赖"
    },
    "message": {
      "en": "Entry {entry} requires service key {key}, which has no provider.",
      "zh-CN": "条目 {entry} 请求的服务键 {key} 无人提供。"
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
  },
  "REGISTRATION_CONFLICT": {
    "code": "REGISTRATION_CONFLICT",
    "kind": "business",
    "status": 409,
    "type": "https://qian.dev/problems/business/REGISTRATION_CONFLICT",
    "owner": "qian.runtime",
    "retryable": false,
    "title": {
      "en": "Registration conflict",
      "zh-CN": "注册冲突"
    },
    "message": {
      "en": "Registration {key} is already owned by entry {owner}.",
      "zh-CN": "注册 {key} 已由条目 {owner} 占用。"
    }
  }
} as const;
