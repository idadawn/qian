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
      "type": "string",
      "const": "pg"
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
{
  "runLeaseSeconds": 600
}
```

### 配置 Schema

来源：` plugins/qian.lifecycle/schemas/config.schema.json `。

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "runLeaseSeconds": {
      "type": "integer",
      "minimum": 30,
      "maximum": 3600,
      "default": 600,
      "description": "钩子执行记录的租约秒数：执行中的记录超过租约仍未写结论时，重试可以接管；未过期时同一幂等键的重试返回 LIFECYCLE_IN_PROGRESS。"
    },
    "executor": {
      "description": "runtime.form: service 组件的部署执行器；省略时安装 service 组件返回 LIFECYCLE_DEPLOYMENT_UNAVAILABLE，module 与声明式组件不受影响。",
      "oneOf": [
        {
          "type": "object",
          "properties": {
            "kind": {
              "const": "gitops"
            },
            "repository": {
              "type": "string",
              "description": "部署仓库：不含凭据的 https / ssh 地址或本机绝对路径；推送凭据由宿主环境的 git 凭据助手提供。"
            },
            "branch": {
              "type": "string",
              "default": "main"
            },
            "path": {
              "type": "string",
              "default": "apps/qian-plugins",
              "description": "仓库内放组件片段的目录；目录下的 kustomization.yaml 由执行器维护。"
            },
            "namespace": {
              "type": "string",
              "default": "qian-plugins",
              "pattern": "^[a-z]([-a-z0-9]{0,61}[a-z0-9])?$"
            },
            "workingDirectory": {
              "type": "string",
              "description": "本地工作副本目录（绝对路径）；缺省在系统临时目录下按仓库生成。"
            },
            "healthTimeoutSeconds": {
              "type": "integer",
              "minimum": 1,
              "maximum": 60,
              "default": 5
            },
            "kubernetes": {
              "type": "object",
              "properties": {
                "server": {
                  "type": "string",
                  "pattern": "^https://[^\\s@?#]\u002B$",
                  "description": "K8s API Server 地址，用于读 Deployment 状态与经服务代理访问组件 /health。"
                },
                "token": {
                  "type": "string",
                  "pattern": "^secretRef://platform/[^\\s?#]\u002B$",
                  "x-qian-sensitive": true,
                  "description": "只读令牌的引用：需要目标命名空间 deployments 的 get 与 services/proxy 的 get。"
                },
                "certificateAuthority": {
                  "type": "string",
                  "pattern": "^secretRef://platform/[^\\s?#]\u002B$",
                  "x-qian-sensitive": true,
                  "description": "集群 CA（PEM）的引用；配置后只信任它签发的证书链。"
                }
              },
              "required": [
                "server",
                "token"
              ],
              "additionalProperties": false
            }
          },
          "required": [
            "kind",
            "repository",
            "kubernetes"
          ],
          "additionalProperties": false
        },
        {
          "type": "object",
          "properties": {
            "kind": {
              "const": "local"
            },
            "stateDirectory": {
              "type": "string",
              "description": "开发态期望状态目录（绝对路径）：每个工作负载一个子目录，内容与 GitOps 片段相同。"
            },
            "endpoints": {
              "type": "object",
              "description": "组件端点：键为 组件@版本 或 组件，值为不含路径的 http(s) 地址；缺省 http://127.0.0.1:\u003Cruntime.service.port\u003E。",
              "additionalProperties": {
                "type": "string",
                "pattern": "^https?://[^\\s/@?#]\u002B/?$"
              }
            },
            "healthTimeoutSeconds": {
              "type": "integer",
              "minimum": 1,
              "maximum": 60,
              "default": 5
            }
          },
          "required": [
            "kind",
            "stateDirectory"
          ],
          "additionalProperties": false
        }
      ]
    }
  },
  "additionalProperties": false
}
```

## global/capabilities

组件：` qian.capabilities@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "catalogTtlMs": 1000,
  "receiptMaxBytes": 65536
}
```

### 配置 Schema

来源：` plugins/qian.capabilities/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "catalogTtlMs": {
      "type": "integer",
      "minimum": 0,
      "maximum": 3000,
      "default": 1000,
      "description": "能力目录响应的 ttlMs：客户端可缓存目录列表的毫秒数。上限与权限变更 3 秒内生效的口径一致；执行入口每次重新判定，缓存不是执行授权。"
    },
    "receiptMaxBytes": {
      "type": "integer",
      "minimum": 0,
      "maximum": 1048576,
      "default": 65536,
      "description": "通用执行入口幂等回执保存响应体的上限（UTF-8 字节）。超出时回执只记已执行、不存响应体，同键重放返回 409 CAPABILITY_REPLAY_UNAVAILABLE；0 表示从不保存响应体。上限 1 MiB 与回执表的检查约束一致；数据分级为 pii、finance、secret 的能力不论大小都不保存响应体。"
    }
  },
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
  },
  "initializationCapabilities": []
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
          "type": "string",
          "const": "production"
        },
        "allowHeaderFallback": {
          "type": "boolean",
          "const": false
        }
      },
      "additionalProperties": false,
      "description": "严格生产解析边界的声明；仅支持已验证 tenant_id，禁止请求头兜底。"
    },
    "initializationCapabilities": {
      "type": "array",
      "items": {
        "type": "string",
        "pattern": "^[a-z][a-z0-9.-]*/[a-z][A-Za-z0-9_.-]*$",
        "maxLength": 384
      },
      "uniqueItems": true,
      "default": [],
      "description": "仅全局开户编排可以执行的初始化能力精确标识；不接受通配符。"
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
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "issuer": {
      "type": "string",
      "description": "发行方的规范 issuer，必须与宿主 IdentityClientOptions.Authority 一致（末尾斜杠不计）；只用于核对，不从配置推导发行方地址。"
    },
    "clientId": {
      "type": "string",
      "minLength": 1,
      "maxLength": 200,
      "description": "在发行方登记的客户端标识，必须与宿主 IdentityClientOptions.ClientId 一致。"
    },
    "clientSecret": {
      "type": "string",
      "description": "机密客户端的密钥引用（secretRef，platform 或 dev），第一次换码时经 Config 的密钥策略解析，不写进组件配置；公开客户端省略。",
      "pattern": "^secretRef://(platform|dev)/[^\\s?#]\u002B$",
      "x-qian-sensitive": true
    }
  },
  "required": [
    "issuer",
    "clientId"
  ],
  "additionalProperties": false
}
```

## global/authz

组件：` qian.authz@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

此条目为业务配置骨架，未证明 Provider 已实现或 Ready。

### 生效配置

```json
{
  "advancedMode": false,
  "decisionLogSampling": 1
}
```

### 配置 Schema

来源：` plugins/qian.authz/schemas/config.schema.json `。

```json
{
  "type": "object",
  "properties": {
    "advancedMode": {
      "type": "boolean",
      "const": false,
      "description": "S1 没有高级模式（数据 / 字段权限编译属 S2），Provider 只接受 false。"
    },
    "platformRole": {
      "type": "string",
      "pattern": "^[a-z][a-z0-9_]{0,62}$",
      "maxLength": 63,
      "description": "可选的独立平台授权登录角色，配置后提供平台超管显式旁路；仅 global Provider；凭据由宿主单独绑定，不写入配置。",
      "not": {
        "pattern": "[^a-z0-9_]"
      }
    },
    "decisionLogSampling": {
      "type": "number",
      "minimum": 0,
      "maximum": 1,
      "default": 1,
      "description": "放行判定写入决策审计（decision_logs）的采样比例，0–1，缺省 1 全记；拒绝不受采样影响、必记（PRD 7.5）。"
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/data

组件：` qian.data@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

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
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "dialect": {
      "type": "string",
      "const": "pg",
      "default": "pg"
    },
    "connection": {
      "type": "string",
      "description": "选择由宿主显式绑定此引用的业务上下文；省略时使用各上下文自身的连接。凭据按上下文创建读取，不写入清单。",
      "pattern": "^secretRef://(platform|dev)/[^\\s?#]\u002B$",
      "x-qian-sensitive": true
    },
    "rls": {
      "type": "object",
      "properties": {
        "enforce": {
          "type": "boolean",
          "const": true
        }
      },
      "required": [
        "enforce"
      ],
      "additionalProperties": false
    }
  },
  "additionalProperties": false
}
```

## global/files

组件：` qian.files@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

### 生效配置

```json
{
  "storage": {
    "kind": "local",
    "root": "/var/lib/qian/files",
    "endpoint": "https://platform.example.internal/files/blobs/"
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
          ],
          "description": "按名称选用 Files.Backends 中登记的存储后端。S1 只登记 local；oss / s3 由 S1-FILE-002 接入，选用未登记的后端在挂载时报 INPUT_INVALID。"
        },
        "root": {
          "type": "string",
          "description": "local 必填：对象根目录（绝对路径）。"
        },
        "bucket": {
          "type": "string"
        },
        "endpoint": {
          "type": "string",
          "description": "local 必填：宿主映射直传 / 直下端点的公开基址（绝对 http(s) 地址，路径与 MapQianFiles 的 TransferPath 一致）；oss / s3：对象存储服务端点。"
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
          "minimum": 0,
          "description": "租户字节配额的平台默认值；租户覆盖经租户配置（ITenantSettings）写入，优先于此值。"
        }
      },
      "additionalProperties": false,
      "default": {
        "perTenantBytes": 10737418240
      }
    },
    "policies": {
      "type": "array",
      "description": "平台默认上传策略；组件也可在 Apply 中经 IFileService.Policies 登记，或在自己的 metadata/upload-policies.json 声明本组件的用途（平台宿主经 Registry 汇总，同用途以这里为准）。用途形如 \u003C组件 ID\u003E/\u003C名称\u003E，组件 ID 进入存储键。",
      "items": {
        "type": "object",
        "required": [
          "purpose",
          "allowedTypes",
          "maxSize"
        ],
        "properties": {
          "purpose": {
            "type": "string",
            "maxLength": 128,
            "pattern": "^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)\u002B/[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$"
          },
          "allowedTypes": {
            "type": "array",
            "minItems": 1,
            "maxItems": 64,
            "items": {
              "type": "string"
            }
          },
          "maxSize": {
            "type": "integer",
            "minimum": 1,
            "maximum": 1099511627776
          },
          "maxCount": {
            "type": "integer",
            "minimum": 1,
            "maximum": 2147483647
          }
        },
        "additionalProperties": false
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
    },
    "env": {
      "type": "string"
    }
  },
  "additionalProperties": false,
  "$schema": "https://json-schema.org/draft/2020-12/schema"
}
```

## global/events

组件：` qian.events@1.0.0 `；来源：` bundle ` / ` plugins/qian.bundle.base/config/qian.patch.yml `。

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
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "properties": {
    "broker": {
      "type": "string",
      "description": "RabbitMQ 的 AMQP 地址引用（secretRef），必须与宿主 AddQianEventsBroker 登记的引用一致；凭据只在连接时读取，不写进清单。省略时不核对 broker，是否投递由宿主装配决定。",
      "pattern": "^secretRef://(platform|dev)/[^\\s?#]\u002B$",
      "x-qian-sensitive": true
    },
    "outbox": {
      "type": "object",
      "properties": {
        "lock": {
          "type": "string",
          "description": "Outbox 领取的互斥方式：skip-locked 为各副本以 FOR UPDATE SKIP LOCKED 加租约分摊；redis 在此之外按表取 Redis 租约，同一时刻一个副本投递。须与宿主装配一致。",
          "enum": [
            "skip-locked",
            "redis"
          ],
          "default": "skip-locked"
        }
      },
      "additionalProperties": false
    }
  },
  "additionalProperties": false
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
          "type": "string",
          "enum": [
            "pg"
          ]
        },
        "platformRole": {
          "type": "string",
          "pattern": "^[a-z][a-z0-9_]{0,62}$",
          "maxLength": 63,
          "description": "可选的独立平台审计登录角色，仅允许 global Provider；凭据由宿主单独绑定，不写入配置。",
          "not": {
            "pattern": "[^a-z0-9_]"
          }
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
  "store": "pg",
  "workerRole": "qian_scheduler_worker",
  "leaseSeconds": 30,
  "maxParallel": 16
}
```

### 配置 Schema

来源：` plugins/qian.scheduler/schemas/config.schema.json `。

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "additionalProperties": false,
  "properties": {
    "store": {
      "const": "pg",
      "default": "pg"
    },
    "workerRole": {
      "type": "string",
      "pattern": "^[a-z][a-z0-9_]{0,62}$",
      "default": "qian_scheduler_worker"
    },
    "leaseSeconds": {
      "type": "integer",
      "minimum": 3,
      "maximum": 3600,
      "default": 30
    },
    "maxParallel": {
      "type": "integer",
      "minimum": 1,
      "maximum": 100,
      "default": 16
    }
  }
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

