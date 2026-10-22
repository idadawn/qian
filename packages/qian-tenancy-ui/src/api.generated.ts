// 由 OpenAPI 契约生成；运行对应包的 contracts:generate。
export interface paths {
    "/api/tenancy/tenants": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["Hf958b36220b04fdafafd4ce1bf1cc7bb286010b9174b9603f521db51c2838ed4"];
        put?: never;
        post: operations["H32e213ade311e9ee4ad412d3b841e07f7eb4e5e83c4a8b4f27b75763a085a00e"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tenancy/tenants/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["H7282d39a5a69d6f1f24ed30a899bdd068a7ebf21ccdbff30f76bf9f0c76338a4"];
        put: operations["H0c94440aa57ef71f7214584848f8d938319335c6bc11651d69daea25551ccefb"];
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tenancy/tenants/{id}:activate": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["Hfb5635e6cfff70e8a4c5e59b16dd6743501530f0b6c26b47a6b08fa844dc414b"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tenancy/tenants/{id}:suspend": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["Hd9c36e685d5005ed061faf9b8c30a0874c23b55e72d1465dc922c2f84fc8d7c4"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tenancy/tenants/{id}:resume": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["H4a21ef0f599db0f5482691b331fb045074833c31fe972f1e6d5a222803e665f5"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tenancy/meta/state-machine": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["H0baeea7de8416226f77a77b251f92c9889405ba0e854f1a75fbf5aaa3d605c95"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tenancy/tenants/{id}/settings/{plugin}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["H6e4552e1952535ae2b6d11e52db4e4104a4f85170191ae9715d76c2577f5474a"];
        put: operations["H14c3651c8980a91cd510dc051dcc0866c8a3a316587cbc581ccecbb0e34f56f8"];
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/tenancy/tenants/{id}/quota": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["Hb102428e0a5eb1cef582378078f5c7bc09c86bfa196cb477a9d4478538804837"];
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
        H98ee36e693dce8c0534fa5498aa174271884d6e3c95afffed9488a0902088eff: string | number;
        H0c00afa8f99e817a162168bc2e67eff70c84db8ee09bc5766b13db3c85247d53: string;
        Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36: {
            type: string;
            title: string;
            status: string | number;
            detail?: string | null;
            instance?: string | null;
            code: string;
            message: string;
            trace_id: string;
            pointer?: string | null;
            allowed?: unknown[] | null;
            errors?: {
                code: string;
                message: string;
                pointer: string;
                allowed?: unknown[] | null;
            }[] | null;
        };
        H1ce4c270da9a9fe630df13cec9a88447a330bf39b1633e580ad9f963176ac679: {
            items: {
                /** Format: uuid */
                id: string;
                code: string;
                name: string;
                /** @enum {unknown} */
                isolation: "row" | "schema" | "database";
                /** @enum {unknown} */
                status: "pending" | "trial" | "active" | "grace" | "suspended";
                revision?: string | number;
            }[];
            total?: string | number;
        };
        Hd23762d7d3c9ec88577858c45d6a88fd7f574569381c9fd4921a6fedd762932c: {
            code: string;
            name: string;
            /**
             * @default row
             * @enum {unknown}
             */
            isolation: "row";
            administrator: {
                /** Format: uuid */
                membershipId: string;
                subjectId: string;
            };
            quotas?: {
                /** @default null */
                users: string | number | null;
            } | null;
        };
        H2f6788f1a524f1bc69a07f9acbc97b230a1814abe957a183a21da0331fa4f549: {
            /** Format: uuid */
            id: string;
            code: string;
            name: string;
            /** @enum {unknown} */
            isolation: "row" | "schema" | "database";
            /** @enum {unknown} */
            status: "pending" | "trial" | "active" | "grace" | "suspended";
            revision?: string | number;
        };
        /** Format: uuid */
        H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b: string;
        He002567801fc664e5a652b25459121b07dda574e55a097298d87bb2bba392801: {
            name: string;
            expectedRevision: string | number;
            quotas?: {
                /** @default null */
                users: string | number | null;
            } | null;
        };
        H95536bbad0a04b0614958ee97f65d39079194641141ae8407dc9eb82466a8088: {
            /** @default false */
            trial: boolean;
            /**
             * Format: date-time
             * @default null
             */
            validUntil: string | null;
        };
        /** 租户状态机元数据 */
        H591ab0ca13d5f0bee30459b9d177be3b8ada88c3bfb037965f829113e19a8b4d: {
            states: ("pending" | "trial" | "active" | "grace" | "suspended")[];
            transitions: {
                from: ("pending" | "trial" | "active" | "grace" | "suspended" | "any-s1" | "previous") | ("pending" | "trial" | "active" | "grace" | "suspended")[];
                action: string;
                to: ("pending" | "trial" | "active" | "grace" | "suspended" | "any-s1" | "previous") | ("pending" | "trial" | "active" | "grace" | "suspended")[];
            }[];
            actions: {
                name: string;
                /** @enum {unknown} */
                sideEffect: "read" | "write" | "destructive";
                agent: {
                    expose: boolean;
                    requiresConfirmation: boolean;
                };
                available?: boolean;
                stage?: string;
                idempotent?: boolean;
            }[];
        };
        Hbfacefa4e5e49e3d17f03c7035114384bdd3214d6937f92ddbe1b7ef3b8a571d: {
            pluginId: string;
            entryId: string;
            revision: string | number;
            config: unknown;
        };
        H12431d9d68dd1bacb508db57db17e615b1c4cb8afee0e1a391f951695dd0755c: {
            config: unknown;
            expectedRevision: string | number;
        };
        H4cd901bfb0521795c9c95a0d6d1e44a70978c701384f39df22e57d9008245923: {
            limits: {
                /** @default null */
                users: string | number | null;
            };
            usersUsed: string | number | null;
            usageAvailable: boolean;
        };
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export interface operations {
    Hf958b36220b04fdafafd4ce1bf1cc7bb286010b9174b9603f521db51c2838ed4: {
        parameters: {
            query?: {
                skip?: components["schemas"]["H98ee36e693dce8c0534fa5498aa174271884d6e3c95afffed9488a0902088eff"];
                take?: components["schemas"]["H98ee36e693dce8c0534fa5498aa174271884d6e3c95afffed9488a0902088eff"];
                status?: components["schemas"]["H0c00afa8f99e817a162168bc2e67eff70c84db8ee09bc5766b13db3c85247d53"];
                keyword?: components["schemas"]["H0c00afa8f99e817a162168bc2e67eff70c84db8ee09bc5766b13db3c85247d53"];
            };
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H1ce4c270da9a9fe630df13cec9a88447a330bf39b1633e580ad9f963176ac679"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    H32e213ade311e9ee4ad412d3b841e07f7eb4e5e83c4a8b4f27b75763a085a00e: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["Hd23762d7d3c9ec88577858c45d6a88fd7f574569381c9fd4921a6fedd762932c"];
            };
        };
        responses: {
            /** @description HTTP 201 */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H2f6788f1a524f1bc69a07f9acbc97b230a1814abe957a183a21da0331fa4f549"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    H7282d39a5a69d6f1f24ed30a899bdd068a7ebf21ccdbff30f76bf9f0c76338a4: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H2f6788f1a524f1bc69a07f9acbc97b230a1814abe957a183a21da0331fa4f549"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    H0c94440aa57ef71f7214584848f8d938319335c6bc11651d69daea25551ccefb: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["He002567801fc664e5a652b25459121b07dda574e55a097298d87bb2bba392801"];
            };
        };
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H2f6788f1a524f1bc69a07f9acbc97b230a1814abe957a183a21da0331fa4f549"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    Hfb5635e6cfff70e8a4c5e59b16dd6743501530f0b6c26b47a6b08fa844dc414b: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["H95536bbad0a04b0614958ee97f65d39079194641141ae8407dc9eb82466a8088"];
            };
        };
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H2f6788f1a524f1bc69a07f9acbc97b230a1814abe957a183a21da0331fa4f549"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    Hd9c36e685d5005ed061faf9b8c30a0874c23b55e72d1465dc922c2f84fc8d7c4: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H2f6788f1a524f1bc69a07f9acbc97b230a1814abe957a183a21da0331fa4f549"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    H4a21ef0f599db0f5482691b331fb045074833c31fe972f1e6d5a222803e665f5: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H2f6788f1a524f1bc69a07f9acbc97b230a1814abe957a183a21da0331fa4f549"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    H0baeea7de8416226f77a77b251f92c9889405ba0e854f1a75fbf5aaa3d605c95: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H591ab0ca13d5f0bee30459b9d177be3b8ada88c3bfb037965f829113e19a8b4d"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    H6e4552e1952535ae2b6d11e52db4e4104a4f85170191ae9715d76c2577f5474a: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
                plugin: components["schemas"]["H0c00afa8f99e817a162168bc2e67eff70c84db8ee09bc5766b13db3c85247d53"];
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["Hbfacefa4e5e49e3d17f03c7035114384bdd3214d6937f92ddbe1b7ef3b8a571d"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    H14c3651c8980a91cd510dc051dcc0866c8a3a316587cbc581ccecbb0e34f56f8: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
                plugin: components["schemas"]["H0c00afa8f99e817a162168bc2e67eff70c84db8ee09bc5766b13db3c85247d53"];
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["H12431d9d68dd1bacb508db57db17e615b1c4cb8afee0e1a391f951695dd0755c"];
            };
        };
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["Hbfacefa4e5e49e3d17f03c7035114384bdd3214d6937f92ddbe1b7ef3b8a571d"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
    Hb102428e0a5eb1cef582378078f5c7bc09c86bfa196cb477a9d4478538804837: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H875d18ad73a6647bb01b2262ae6f3d4f4e7320aaf1223e60ad82e25b12f80b6b"];
            };
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H4cd901bfb0521795c9c95a0d6d1e44a70978c701384f39df22e57d9008245923"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["Ha9725c39f8a61813c2c829aa37ed10cb8657265c7c692e19402dbc6efa42bf36"];
                };
            };
        };
    };
}
