// 由 OpenAPI 契约生成；运行对应包的 contracts:generate。
export interface paths {
    "/api/directory/orgs/tree": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * 读取本租户完整组织树
         * @description 按 path 排序的扁平列表，以 parent_id 关联；根组织没有 parent_id。调用方须持本租户有效成员会话。
         */
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description 组织列表 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["OrganizationList"];
                    };
                };
                401: components["responses"]["Problem"];
                403: components["responses"]["Problem"];
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
    "/api/directory/members": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * 分页读取本租户成员
         * @description 按账号、成员 id 排序；不含已删除与已清除的成员，也不合成平台管理员。
         */
        get: {
            parameters: {
                query?: {
                    offset?: components["parameters"]["Offset"];
                    limit?: components["parameters"]["Limit"];
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description 成员当前页 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["MemberList"];
                    };
                };
                400: components["responses"]["Problem"];
                401: components["responses"]["Problem"];
                403: components["responses"]["Problem"];
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
    "/api/directory/orgs/{id}/members": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * 分页读取组织下的成员
         * @description descendants 为 true 时包含整棵子树的成员。
         */
        get: {
            parameters: {
                query?: {
                    descendants?: boolean;
                    offset?: components["parameters"]["Offset"];
                    limit?: components["parameters"]["Limit"];
                };
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description 成员当前页 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["MemberList"];
                    };
                };
                400: components["responses"]["Problem"];
                401: components["responses"]["Problem"];
                403: components["responses"]["Problem"];
                404: components["responses"]["Problem"];
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
    "/api/directory/org-types": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /** 读取租户组织类型字典 */
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description 组织类型 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["OrganizationTypeList"];
                    };
                };
                401: components["responses"]["Problem"];
                403: components["responses"]["Problem"];
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
    "/api/authz/roles": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * 读取本租户角色目录（草案）
         * @description 内置角色与自定义角色；角色以 code 标识，授予与撤销沿用 ITenantRoleGrant 的 roleCode。
         */
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description 角色列表 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["RoleList"];
                    };
                };
                401: components["responses"]["Problem"];
                403: components["responses"]["Problem"];
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
    "/api/authz/permissions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        /**
         * 读取本租户可授予的权限点目录（草案）
         * @description 条目形状同 GET /api/authz/me/permissions 去掉 allowed / reason；resources 给出资源类型名称，供授权树分组显示。
         */
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description 权限点目录 */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "application/json": components["schemas"]["PermissionCatalog"];
                    };
                };
                401: components["responses"]["Problem"];
                403: components["responses"]["Problem"];
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
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        /** @description 组织投影；path 以斜线包围祖先链 UUID，depth 从 1 开始。 */
        Organization: {
            /** Format: uuid */
            id: string;
            name: string;
            type_code: string;
            /**
             * Format: uuid
             * @description 根组织省略。
             */
            parent_id?: string;
            path: string;
            depth: number;
            /** Format: uuid */
            manager_membership_id?: string;
            /** Format: uuid */
            responsible_position_id?: string;
        };
        OrganizationList: {
            items: components["schemas"]["Organization"][];
        };
        OrganizationType: {
            code: string;
            name: string;
        };
        OrganizationTypeList: {
            items: components["schemas"]["OrganizationType"][];
        };
        /** @description 租户成员；id 是目录侧的成员标识，person_id 是自然人标识（即令牌 sub，也是授权侧 user 主体的 subjectId）。 */
        Membership: {
            /** Format: uuid */
            id: string;
            /** Format: uuid */
            person_id: string;
            /** Format: uuid */
            tenant_id: string;
            display_name: string;
            /** @enum {string} */
            status: "active" | "disabled" | "locked";
            is_tenant_administrator: boolean;
            account: string;
            /** Format: int64 */
            identity_revision: number;
        };
        DirectoryMember: {
            membership: components["schemas"]["Membership"];
            deleted: boolean;
        };
        MemberList: {
            items: components["schemas"]["DirectoryMember"][];
        };
        /** @description 角色目录条目（草案）。内置角色为 tenant.owner / tenant.admin / tenant.developer / tenant.user，界面按 code 取文案；name 供自定义角色显示。 */
        Role: {
            code: string;
            name?: string;
            builtin: boolean;
            /** @description 来自插件默认角色模板时为策略包标识。 */
            sourcePack?: string;
        };
        RoleList: {
            items: components["schemas"]["Role"][];
        };
        /** @description 权限点（PRD 7.5 permission_definitions）。permission 为全名 <plugin-id>:<resource>:<action>，resource 为带组件前缀的资源类型。 */
        PermissionDefinition: {
            permission: string;
            resource: string;
            action: string;
            /** @enum {string} */
            risk: "low" | "medium" | "high" | "critical";
            stepUp: boolean;
            terminals: string[];
        };
        /** @description 资源类型（PRD 7.5 resource_types）。 */
        ResourceType: {
            resource: string;
            name: string;
        };
        PermissionCatalog: {
            items: components["schemas"]["PermissionDefinition"][];
            resources: components["schemas"]["ResourceType"][];
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
    responses: {
        /** @description ErrorDetail */
        Problem: {
            headers: {
                [name: string]: unknown;
            };
            content: {
                "application/problem+json": components["schemas"]["error-detail.schema"];
            };
        };
    };
    parameters: {
        Offset: number;
        Limit: number;
    };
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
