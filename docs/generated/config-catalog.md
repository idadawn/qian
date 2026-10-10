# 配置目录

由 `qian docs generate` 派生；profile：` platform `。请修改输入后重新生成。

配置来自最终活动条目；整段替换后再补 Schema 默认值。secretRef 原样保留，敏感 Schema 的示例和默认值不导出。

## global/registry

组件：` qian.registry@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "store": "pg"
}
```

### 配置 Schema

来源：` plugins/qian.registry/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "store": {
      "type": "string"
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/lifecycle

组件：` qian.lifecycle@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{}
```

### 配置 Schema

来源：` plugins/qian.lifecycle/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/capabilities

组件：` qian.capabilities@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{}
```

### 配置 Schema

来源：` plugins/qian.capabilities/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/hooks

组件：` qian.hooks@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{}
```

### 配置 Schema

来源：` plugins/qian.hooks/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/tenancy

组件：` qian.tenancy@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "resolver": {
    "mode": "production",
    "allowHeaderFallback": false
  }
}
```

### 配置 Schema

来源：` plugins/qian.tenancy/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "resolver": {
      "type": "object",
      "properties": {
        "mode": {
          "type": "string"
        },
        "allowHeaderFallback": {
          "type": "boolean"
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/identity

组件：` qian.identity.client@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "issuer": "https://id.example.internal",
  "clientId": "qian-platform",
  "clientSecret": "secretRef://platform/identity/client-secret"
}
```

### 配置 Schema

来源：` plugins/qian.identity.client/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "issuer": {
      "type": "string"
    },
    "clientId": {
      "type": "string"
    },
    "clientSecret": {
      "type": "string",
      "x-qian-sensitive": true
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/authz

组件：` qian.authz@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "advancedMode": false
}
```

### 配置 Schema

来源：` plugins/qian.authz/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "advancedMode": {
      "type": "boolean"
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/data

组件：` qian.data@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "dialect": "pg",
  "connection": "secretRef://platform/pg/qian-platform",
  "rls": {
    "enforce": true
  }
}
```

### 配置 Schema

来源：` plugins/qian.data/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "dialect": {
      "type": "string"
    },
    "connection": {
      "type": "string",
      "x-qian-sensitive": true
    },
    "rls": {
      "type": "object",
      "properties": {
        "enforce": {
          "type": "boolean"
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/files

组件：` qian.files@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "storage": {
    "kind": "local",
    "root": "/var/lib/qian/files"
  },
  "quota": {
    "perTenantBytes": 10737418240
  }
}
```

### 配置 Schema

来源：` plugins/qian.files/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "storage": {
      "type": "object",
      "required": [
        "kind"
      ],
      "properties": {
        "kind": {
          "enum": [
            "local",
            "oss",
            "s3"
          ]
        },
        "root": {
          "type": "string"
        },
        "bucket": {
          "type": "string"
        },
        "endpoint": {
          "type": "string"
        },
        "accessKey": {
          "type": "string",
          "x-qian-sensitive": true
        },
        "secretKey": {
          "type": "string",
          "x-qian-sensitive": true
        }
      },
      "additionalProperties": false
    },
    "quota": {
      "type": "object",
      "properties": {
        "perTenantBytes": {
          "type": "integer",
          "minimum": 0
        }
      },
      "additionalProperties": false,
      "default": {
        "perTenantBytes": 10737418240
      }
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "required": [
    "storage"
  ]
}
```

## global/config

组件：` qian.config.apollo@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "meta": "http://apollo-config.apollo:8080",
  "appId": "qian-platform",
  "namespaces": [
    "application",
    "qian.common.trust"
  ]
}
```

### 配置 Schema

来源：` plugins/qian.config.apollo/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "meta": {
      "type": "string"
    },
    "appId": {
      "type": "string"
    },
    "namespaces": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/events

组件：` qian.events@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "broker": "secretRef://platform/rabbitmq/url",
  "outbox": {
    "lock": "redis"
  }
}
```

### 配置 Schema

来源：` plugins/qian.events/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "broker": {
      "type": "string",
      "x-qian-sensitive": true
    },
    "outbox": {
      "type": "object",
      "properties": {
        "lock": {
          "type": "string"
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/audit

组件：` qian.audit@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "chain": {
    "store": "pg"
  }
}
```

### 配置 Schema

来源：` plugins/qian.audit/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "chain": {
      "type": "object",
      "properties": {
        "store": {
          "type": "string"
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/observability

组件：` qian.observability@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "otlp": "http://otel-collector.observability:4317"
}
```

### 配置 Schema

来源：` plugins/qian.observability/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "otlp": {
      "type": "string"
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/notify

组件：` qian.notify@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "channels": [
    "inbox",
    "email",
    "sms"
  ]
}
```

### 配置 Schema

来源：` plugins/qian.notify/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "channels": {
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/notify.inbox

组件：` qian.notify.channel.inbox@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{}
```

### 配置 Schema

来源：` plugins/qian.notify.channel.inbox/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/notify.email

组件：` qian.notify.channel.email@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "smtp": "secretRef://platform/smtp/url"
}
```

### 配置 Schema

来源：` plugins/qian.notify.channel.email/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "smtp": {
      "type": "string",
      "x-qian-sensitive": true
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/notify.sms

组件：` qian.notify.channel.sms@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "provider": "secretRef://platform/sms/credential"
}
```

### 配置 Schema

来源：` plugins/qian.notify.channel.sms/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "provider": {
      "type": "string",
      "x-qian-sensitive": true
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/scheduler

组件：` qian.scheduler@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "store": "pg"
}
```

### 配置 Schema

来源：` plugins/qian.scheduler/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "store": {
      "type": "string"
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/realtime

组件：` qian.realtime@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "transport": "sse"
}
```

### 配置 Schema

来源：` plugins/qian.realtime/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "transport": {
      "type": "string"
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/entitlement

组件：` qian.entitlement@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{}
```

### 配置 Schema

来源：` plugins/qian.entitlement/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/shell

组件：` qian.shell@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.shell/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{}
```

### 配置 Schema

来源：` plugins/qian.shell/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {},
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

