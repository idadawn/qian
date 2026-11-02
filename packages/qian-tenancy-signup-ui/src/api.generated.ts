// 由 OpenAPI 契约生成；运行对应包的 contracts:generate。
export interface paths {
    "/api/signup/verifications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["H2b08b2df67fa18e644cdceb83644a74fed61a270f4a2029d4c016215a071c47b"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/session": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["H35fdba65ca94d7f8369838eba06c726039d8e86f100c58e6455ac0369dcfa281"];
        put?: never;
        post: operations["H5eaae600d40b446d4138866d8fe63ab537c2c76e7ee4d1f9462dbd97676e555c"];
        delete: operations["H6cb7eed1a54ac7a662ff44934cab0c5f521328ffb3fb4ffe868ea4964bcede47"];
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/code-availability": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["Hc00ba6c8499247b237a0bd0021879ebbe9be3d6f7c0f9db9492c5c70cd1a3aa3"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/me/applications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["Hcb11751edfe0b115fc9c3c8c4c78b80255cbafbce9de4e34017f3ea56c4c596d"];
        put?: never;
        post: operations["Ha2beb20fcb0a6f4117b0c76e8f3f5629e48f78db22c88798cdcf6eb05ce33681"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/me/applications/{id}:withdraw": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["H197419483f0561d65bb80f3dbf56e8b2d10f553fe1ffbc78827ce64a714f6e9a"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/applications": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["H1bef30d64f0861eadf936c01a97b31416e5d00da80dff3d7291787346496b250"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/applications/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["H351aedd37fcb9f3c2fad756b72a2b812af952b90846f7c96dd0144a459fdc3da"];
        put?: never;
        post?: never;
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/applications/{id}:approve": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["H21b9cc8b44bd544b061031ffef2204788e40b43b0a0c21abbcef7cb38d7d6ef0"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/applications/{id}:reject": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["Ha788f63d739e8e50d5f8b0e905f1f1fea28589d117c82f7e88386059509f23a8"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/applications/{id}:retry": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get?: never;
        put?: never;
        post: operations["H928b9def47e80919b7b6334dcf69a871ed253f19f9e3628f10b939fc0a862f66"];
        delete?: never;
        options?: never;
        head?: never;
        patch?: never;
        trace?: never;
    };
    "/api/signup/meta/state-machine": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: operations["Hfa10adccf93161829ae9521d679e3f851319bb515dc2fbe60d5918832c899615"];
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
        H22609365d7f2b322c527d816a7e5bd5bef0ca840e4019b4106c4ac99cb4953c3: {
            phone: string;
        };
        H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b: {
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
        Hdb39c4a47ba1788bff5adeefe58bd971a462298bf4cb483704ed74672d175acf: {
            /** Format: uuid */
            id: string;
            /** Format: date-time */
            expiresAt: string;
            /** Format: date-time */
            resendAfter: string;
        };
        Hf6ad59a1e1f3e7cb15416225b5c19239e585e31230edaa7e5a3472fa8d5b95a8: {
            /** Format: uuid */
            verificationId: string;
            code: string;
        };
        H9f6fc38502660c8634769cb4f9d1886ef0b6ed9457c840a6d859a2c71e4ef79b: {
            phone: string;
            existingAccount: boolean;
            displayName?: string | null;
        };
        Hd9eacec9f1b8feaf92c605e7d4531376b3994d3a184f18dbf002d8d54e37055c: string;
        Hcf30c0de7f4fe1d4df46f26fc72a4ea0c223474810a768107050e0bbbdbf11a9: {
            code: string;
            available: boolean;
            reason?: string | null;
        };
        H3ee8dee1d897e91336d0c5265ea9acd041139a48ddfd03520ea9f25ebc2d46f9: {
            items: {
                /** Format: uuid */
                id: string;
                number: string;
                /** @enum {unknown} */
                status: "submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn";
                companyName: string;
                code: string;
                size: string;
                contactName: string;
                phone: string;
                /** Format: date-time */
                submittedAt: string;
                industry?: string | null;
                description?: string | null;
                email?: string | null;
                /** Format: date-time */
                completedAt?: string | null;
                /** @enum {unknown} */
                plan?: "trial" | "formal" | null;
                /** Format: date-time */
                validUntil?: string | null;
                rejectReason?: string | null;
            }[];
        };
        H311b6b02569f39bf1c33eea8167febd8b5e02a93d575c38d09bcc7adb418b73f: {
            companyName: string;
            code: string;
            /** @enum {string} */
            size: "1-20" | "21-100" | "101-500" | "501-1000" | "1000+";
            /** @enum {boolean} */
            acceptTerms: true;
            displayName?: string | null;
            password?: string | null;
            email?: string | null;
            industry?: string | null;
            description?: string | null;
        };
        Hcc4638c75c8573ef91198006e2d820300d79d7ac1856903698e2d511ce0efbe2: {
            /** Format: uuid */
            id: string;
            number: string;
            /** @enum {unknown} */
            status: "submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn";
            companyName: string;
            code: string;
            size: string;
            contactName: string;
            phone: string;
            /** Format: date-time */
            submittedAt: string;
            industry?: string | null;
            description?: string | null;
            email?: string | null;
            /** Format: date-time */
            completedAt?: string | null;
            /** @enum {unknown} */
            plan?: "trial" | "formal" | null;
            /** Format: date-time */
            validUntil?: string | null;
            rejectReason?: string | null;
        };
        /** Format: uuid */
        H0ef39f8f8ea8707ab397a03addd15e846001a526b650ddb130f26af04cfe07d2: string;
        H5feca5d855beb5e05288db2b462eaf7479abb6c2272885e6031a3b5eb7b491c9: string | number;
        Hc621aab487b7439637c2018d89e67bc930ea5bf35ef3b8f017912212ab50004b: {
            items: {
                /** Format: uuid */
                id: string;
                number: string;
                /** @enum {unknown} */
                status: "submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn";
                companyName: string;
                code: string;
                size: string;
                contactName: string;
                phone: string;
                /** Format: date-time */
                submittedAt: string;
                check: {
                    kind: string;
                    level: string;
                    reason?: string | null;
                    count?: string | number | null;
                } | null;
                industry?: string | null;
            }[];
            total: string | number;
            counts: {
                all: string | number;
                submitted: string | number;
                provisioning: string | number;
                approved: string | number;
                failed: string | number;
                rejected: string | number;
                withdrawn: string | number;
            };
        };
        H299b158008bcf614fb9bd780dc50e2886aa3b4b258b68a93229f19c45eaa5ab7: {
            /** Format: uuid */
            id: string;
            number: string;
            /** @enum {unknown} */
            status: "submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn";
            company: {
                name: string;
                code: string;
                size: string;
                industry?: string | null;
                description?: string | null;
            };
            applicant: {
                /** Format: uuid */
                subjectId: string;
                contactName: string;
                phone: string;
                existingAccount: boolean;
                email?: string | null;
            };
            checks: {
                kind: string;
                level: string;
                reason?: string | null;
                count?: string | number | null;
            }[];
            defaults: {
                trialDays: string | number;
                users: string | number;
            };
            /** Format: date-time */
            submittedAt: string;
            /** Format: date-time */
            completedAt?: string | null;
            review?: {
                reviewer: string;
                /** Format: date-time */
                reviewedAt: string;
                /** @enum {unknown} */
                plan?: "trial" | "formal" | null;
                /** Format: date-time */
                validUntil?: string | null;
                users?: string | number | null;
                rejectReason?: string | null;
                note?: string | null;
            } | null;
            provisioning?: {
                attempts: string | number;
                steps: {
                    step: string;
                    status: string;
                    attempts: string | number;
                    reason?: string | null;
                }[];
                /** Format: uuid */
                tenantId?: string | null;
                /** Format: uuid */
                membershipId?: string | null;
                failureReason?: string | null;
            } | null;
        };
        H15536eaf860b83b7a6ae6d1f646823ec40463e417bd0b28f030d435443b1c120: {
            /** @enum {unknown} */
            plan: "trial" | "formal";
            code?: string | null;
            /** Format: date-time */
            validUntil?: string | null;
            users?: string | number | null;
            note?: string | null;
        };
        H4f284d44c172d92bf2d2e81684a4234da965d31e84e01c77044aa8a9c494be09: {
            reason: string;
            note?: string | null;
        };
        H62af787f6393dc0dfcf2105d18c9525625d6d52c8176ec06c25997cc6e47f802: {
            states: ("submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn")[];
            terminal: ("submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn")[];
            transitions: {
                from: ("submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn")[];
                action: string;
                /** @enum {unknown} */
                to: "submitted" | "provisioning" | "approved" | "failed" | "rejected" | "withdrawn";
            }[];
            actions: {
                name: string;
                actor: string;
                sideEffect: string;
                agent: {
                    expose: boolean;
                    requiresConfirmation: boolean;
                };
            }[];
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
    H2b08b2df67fa18e644cdceb83644a74fed61a270f4a2029d4c016215a071c47b: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["H22609365d7f2b322c527d816a7e5bd5bef0ca840e4019b4106c4ac99cb4953c3"];
            };
        };
        responses: {
            /** @description HTTP 202 */
            202: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["Hdb39c4a47ba1788bff5adeefe58bd971a462298bf4cb483704ed74672d175acf"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 415 */
            415: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 429 */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 503 */
            503: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H35fdba65ca94d7f8369838eba06c726039d8e86f100c58e6455ac0369dcfa281: {
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
                    "application/json": components["schemas"]["H9f6fc38502660c8634769cb4f9d1886ef0b6ed9457c840a6d859a2c71e4ef79b"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H5eaae600d40b446d4138866d8fe63ab537c2c76e7ee4d1f9462dbd97676e555c: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["Hf6ad59a1e1f3e7cb15416225b5c19239e585e31230edaa7e5a3472fa8d5b95a8"];
            };
        };
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H9f6fc38502660c8634769cb4f9d1886ef0b6ed9457c840a6d859a2c71e4ef79b"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 415 */
            415: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H6cb7eed1a54ac7a662ff44934cab0c5f521328ffb3fb4ffe868ea4964bcede47: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody?: never;
        responses: {
            /** @description HTTP 204 */
            204: {
                headers: {
                    [name: string]: unknown;
                };
                content?: never;
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    Hc00ba6c8499247b237a0bd0021879ebbe9be3d6f7c0f9db9492c5c70cd1a3aa3: {
        parameters: {
            query: {
                code: components["schemas"]["Hd9eacec9f1b8feaf92c605e7d4531376b3994d3a184f18dbf002d8d54e37055c"];
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
                    "application/json": components["schemas"]["Hcf30c0de7f4fe1d4df46f26fc72a4ea0c223474810a768107050e0bbbdbf11a9"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 429 */
            429: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    Hcb11751edfe0b115fc9c3c8c4c78b80255cbafbce9de4e34017f3ea56c4c596d: {
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
                    "application/json": components["schemas"]["H3ee8dee1d897e91336d0c5265ea9acd041139a48ddfd03520ea9f25ebc2d46f9"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    Ha2beb20fcb0a6f4117b0c76e8f3f5629e48f78db22c88798cdcf6eb05ce33681: {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["H311b6b02569f39bf1c33eea8167febd8b5e02a93d575c38d09bcc7adb418b73f"];
            };
        };
        responses: {
            /** @description HTTP 201 */
            201: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["Hcc4638c75c8573ef91198006e2d820300d79d7ac1856903698e2d511ce0efbe2"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 415 */
            415: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 422 */
            422: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H197419483f0561d65bb80f3dbf56e8b2d10f553fe1ffbc78827ce64a714f6e9a: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H0ef39f8f8ea8707ab397a03addd15e846001a526b650ddb130f26af04cfe07d2"];
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
                    "application/json": components["schemas"]["Hcc4638c75c8573ef91198006e2d820300d79d7ac1856903698e2d511ce0efbe2"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 415 */
            415: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H1bef30d64f0861eadf936c01a97b31416e5d00da80dff3d7291787346496b250: {
        parameters: {
            query?: {
                status?: components["schemas"]["Hd9eacec9f1b8feaf92c605e7d4531376b3994d3a184f18dbf002d8d54e37055c"];
                keyword?: components["schemas"]["Hd9eacec9f1b8feaf92c605e7d4531376b3994d3a184f18dbf002d8d54e37055c"];
                size?: components["schemas"]["Hd9eacec9f1b8feaf92c605e7d4531376b3994d3a184f18dbf002d8d54e37055c"];
                skip?: components["schemas"]["H5feca5d855beb5e05288db2b462eaf7479abb6c2272885e6031a3b5eb7b491c9"];
                take?: components["schemas"]["H5feca5d855beb5e05288db2b462eaf7479abb6c2272885e6031a3b5eb7b491c9"];
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
                    "application/json": components["schemas"]["Hc621aab487b7439637c2018d89e67bc930ea5bf35ef3b8f017912212ab50004b"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H351aedd37fcb9f3c2fad756b72a2b812af952b90846f7c96dd0144a459fdc3da: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H0ef39f8f8ea8707ab397a03addd15e846001a526b650ddb130f26af04cfe07d2"];
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
                    "application/json": components["schemas"]["H299b158008bcf614fb9bd780dc50e2886aa3b4b258b68a93229f19c45eaa5ab7"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H21b9cc8b44bd544b061031ffef2204788e40b43b0a0c21abbcef7cb38d7d6ef0: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H0ef39f8f8ea8707ab397a03addd15e846001a526b650ddb130f26af04cfe07d2"];
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["H15536eaf860b83b7a6ae6d1f646823ec40463e417bd0b28f030d435443b1c120"];
            };
        };
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H299b158008bcf614fb9bd780dc50e2886aa3b4b258b68a93229f19c45eaa5ab7"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    Ha788f63d739e8e50d5f8b0e905f1f1fea28589d117c82f7e88386059509f23a8: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H0ef39f8f8ea8707ab397a03addd15e846001a526b650ddb130f26af04cfe07d2"];
            };
            cookie?: never;
        };
        requestBody: {
            content: {
                "application/json": components["schemas"]["H4f284d44c172d92bf2d2e81684a4234da965d31e84e01c77044aa8a9c494be09"];
            };
        };
        responses: {
            /** @description HTTP 200 */
            200: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/json": components["schemas"]["H299b158008bcf614fb9bd780dc50e2886aa3b4b258b68a93229f19c45eaa5ab7"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    H928b9def47e80919b7b6334dcf69a871ed253f19f9e3628f10b939fc0a862f66: {
        parameters: {
            query?: never;
            header?: never;
            path: {
                id: components["schemas"]["H0ef39f8f8ea8707ab397a03addd15e846001a526b650ddb130f26af04cfe07d2"];
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
                    "application/json": components["schemas"]["H299b158008bcf614fb9bd780dc50e2886aa3b4b258b68a93229f19c45eaa5ab7"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
    Hfa10adccf93161829ae9521d679e3f851319bb515dc2fbe60d5918832c899615: {
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
                    "application/json": components["schemas"]["H62af787f6393dc0dfcf2105d18c9525625d6d52c8176ec06c25997cc6e47f802"];
                };
            };
            /** @description HTTP 400 */
            400: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 401 */
            401: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 403 */
            403: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 404 */
            404: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
            /** @description HTTP 409 */
            409: {
                headers: {
                    [name: string]: unknown;
                };
                content: {
                    "application/problem+json": components["schemas"]["H3a7dd4c572560b85deffa589d0fe12f854376ea7204ac901a6892001955f757b"];
                };
            };
        };
    };
}
