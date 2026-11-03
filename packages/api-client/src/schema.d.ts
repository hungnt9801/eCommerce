export interface paths {
    "/api/v1/orders": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Page?: number;
                    PageSize?: number;
                    SkipCount?: number;
                    Tab?: string;
                    Q?: string;
                    FulfillmentStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
                    Type?: components["schemas"]["eCommerce.OrderType"];
                    Source?: components["schemas"]["eCommerce.OrderSource"];
                    WarehouseId?: string;
                    FromUtc?: string;
                    ToUtc?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderListItemDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "application/json": components["schemas"]["Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderListItemDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "text/json": components["schemas"]["Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderListItemDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
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
    "/api/v1/orders/tabs": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Page?: number;
                    PageSize?: number;
                    SkipCount?: number;
                    Tab?: string;
                    Q?: string;
                    FulfillmentStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
                    Type?: components["schemas"]["eCommerce.OrderType"];
                    Source?: components["schemas"]["eCommerce.OrderSource"];
                    WarehouseId?: string;
                    FromUtc?: string;
                    ToUtc?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderTabCountsDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderTabCountsDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderTabCountsDto"];
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
    "/api/v1/orders/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderDetailDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderDetailDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderDetailDto"];
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
    "/api/v1/orders/{id}/history": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    skipCount?: number;
                    maxResultCount?: number;
                };
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderStatusHistoryDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "application/json": components["schemas"]["Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderStatusHistoryDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "text/json": components["schemas"]["Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderStatusHistoryDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
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
    "/api/v1/orders/{id}/transitions": {
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
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["eCommerce.HttpApi.Controllers.ChangeOrderStatusBody"];
                    "text/json": components["schemas"]["eCommerce.HttpApi.Controllers.ChangeOrderStatusBody"];
                    "application/*+json": components["schemas"]["eCommerce.HttpApi.Controllers.ChangeOrderStatusBody"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderStatusDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderStatusDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderStatusDto"];
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
    "/api/v1/orders/{id}/cancel": {
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
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["eCommerce.HttpApi.Controllers.CancelOrderBody"];
                    "text/json": components["schemas"]["eCommerce.HttpApi.Controllers.CancelOrderBody"];
                    "application/*+json": components["schemas"]["eCommerce.HttpApi.Controllers.CancelOrderBody"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderStatusDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderStatusDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Sales.OrderStatusDto"];
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
    "/api/v1/products": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    Q?: string;
                    Status?: string;
                    Page?: number;
                    PageSize?: number;
                    Sort?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.HttpApi.Dtos.ProductListItemDto, eCommerce.HttpApi, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.HttpApi.Dtos.ProductListItemDto, eCommerce.HttpApi, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.HttpApi.Dtos.ProductListItemDto, eCommerce.HttpApi, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                    };
                };
            };
        };
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["eCommerce.Catalog.Dtos.CreateProductDto"];
                    "text/json": components["schemas"]["eCommerce.Catalog.Dtos.CreateProductDto"];
                    "application/*+json": components["schemas"]["eCommerce.Catalog.Dtos.CreateProductDto"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Dtos.ProductDetailDto"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Dtos.ProductDetailDto"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Dtos.ProductDetailDto"];
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
    "/api/v1/products/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Dtos.ProductDetailDto"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Dtos.ProductDetailDto"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Dtos.ProductDetailDto"];
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
    "/api/v1/products/lookup": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    q?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Dtos.ProductLookupResultDto"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Dtos.ProductLookupResultDto"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Dtos.ProductLookupResultDto"];
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
    "/api/v1/stock/balances": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    warehouseId?: string;
                    sku?: string;
                    page?: number;
                    pageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockBalanceDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockBalanceDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockBalanceDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
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
    "/api/v1/stock/bills": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    direction?: components["schemas"]["eCommerce.Inventory.StockDocumentDirection"];
                    status?: components["schemas"]["eCommerce.Inventory.StockDocumentStatus"];
                    mode?: components["schemas"]["eCommerce.Inventory.StockDocumentMode"];
                    warehouseId?: string;
                    fromUtc?: string;
                    toUtc?: string;
                    page?: number;
                    pageSize?: number;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockDocumentDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockDocumentDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockDocumentDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"];
                    };
                };
            };
        };
        put?: never;
        post: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: {
                content: {
                    "application/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.CreateStockDocumentDto"];
                    "text/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.CreateStockDocumentDto"];
                    "application/*+json": components["schemas"]["eCommerce.Application.Contracts.Inventory.CreateStockDocumentDto"];
                };
            };
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
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
    "/api/v1/stock/bills/{id}": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
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
    "/api/v1/stock/bills/{id}/lines": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path: {
                    id: string;
                };
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentLineDto"][];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentLineDto"][];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentLineDto"][];
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
    "/api/v1/stock/bills/{id}/post": {
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
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Inventory.PostStockDocumentResultDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.PostStockDocumentResultDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.PostStockDocumentResultDto"];
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
    "/api/v1/stock/bills/{id}/cancel": {
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
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
                        "application/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
                        "text/json": components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"];
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
    "/api/v1/system/ping": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+PingResult"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+PingResult"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+PingResult"];
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
    "/api/v1/system/me": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: never;
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+MeResult"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+MeResult"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+MeResult"];
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
    "/api/v1/system/permissions": {
        parameters: {
            query?: never;
            header?: never;
            path?: never;
            cookie?: never;
        };
        get: {
            parameters: {
                query?: {
                    names?: string;
                };
                header?: never;
                path?: never;
                cookie?: never;
            };
            requestBody?: never;
            responses: {
                /** @description OK */
                200: {
                    headers: {
                        [name: string]: unknown;
                    };
                    content: {
                        "text/plain": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+PermissionsResult"];
                        "application/json": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+PermissionsResult"];
                        "text/json": components["schemas"]["eCommerce.HttpApi.Controllers.SystemController+PermissionsResult"];
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
}
export type webhooks = Record<string, never>;
export interface components {
    schemas: {
        "System.ValueTuple`2[[System.String, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e],[System.Int32, System.Private.CoreLib, Version=10.0.0.0, Culture=neutral, PublicKeyToken=7cec85d7bea7798e]]": Record<string, never>;
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Inventory.HanMucKenhDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Application.Contracts.Inventory.HanMucKenhDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Inventory.StockBalanceDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Application.Contracts.Inventory.StockBalanceDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Inventory.StockDocumentDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderListItemDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Application.Contracts.Sales.OrderListItemDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Application.Contracts.Sales.OrderStatusHistoryDto, eCommerce.Sales.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Application.Contracts.Sales.OrderStatusHistoryDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Catalog.Dtos.BrandDto, eCommerce.Catalog.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Catalog.Dtos.BrandDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Catalog.Dtos.CategoryDto, eCommerce.Catalog.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Catalog.Dtos.CategoryDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Catalog.Dtos.HienThiKenhDto, eCommerce.Catalog.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Catalog.Dtos.HienThiKenhDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Catalog.Dtos.ProductDto, eCommerce.Catalog.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Catalog.Dtos.ProductDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Catalog.Dtos.ProductMediaDto, eCommerce.Catalog.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Catalog.Dtos.ProductMediaDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Channels.Dtos.ChannelShopDto, eCommerce.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Channels.Dtos.ChannelShopDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Customers.CustomerDto, eCommerce.Customers.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Customers.CustomerDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Customers.CustomerGroupDto, eCommerce.Customers.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Customers.CustomerGroupDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Inventory.DongLechDto, eCommerce.Reconciliation.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Inventory.DongLechDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Inventory.LuotDoiSoatDto, eCommerce.Reconciliation.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Inventory.LuotDoiSoatDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Messaging.DlqDto, eCommerce.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Messaging.DlqDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Reconciliation.Dtos.PaymentDiscrepancyDto, eCommerce.Reconciliation.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Reconciliation.Dtos.PaymentDiscrepancyDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Reconciliation.Dtos.PaymentReconciliationLineDto, eCommerce.Reconciliation.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Reconciliation.Dtos.PaymentReconciliationLineDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.Application.Dtos.PagedResultDto`1[[eCommerce.Reconciliation.Dtos.PaymentReconciliationRunDto, eCommerce.Reconciliation.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Reconciliation.Dtos.PaymentReconciliationRunDto"][] | null;
            /** Format: int64 */
            totalCount?: number;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationAuthConfigurationDto": {
            grantedPolicies?: {
                [key: string]: boolean;
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationConfigurationDto": {
            localization?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationLocalizationConfigurationDto"];
            auth?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationAuthConfigurationDto"];
            setting?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationSettingConfigurationDto"];
            currentUser?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.CurrentUserDto"];
            features?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationFeatureConfigurationDto"];
            globalFeatures?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationGlobalFeatureConfigurationDto"];
            multiTenancy?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.MultiTenancy.MultiTenancyInfoDto"];
            currentTenant?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.MultiTenancy.CurrentTenantDto"];
            timing?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.TimingDto"];
            clock?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ClockDto"];
            objectExtensions?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ObjectExtensionsDto"];
            extraProperties?: {
                [key: string]: unknown;
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationFeatureConfigurationDto": {
            values?: {
                [key: string]: string | null;
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationGlobalFeatureConfigurationDto": {
            enabledFeatures?: string[] | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationLocalizationConfigurationDto": {
            values?: {
                [key: string]: {
                    [key: string]: string;
                };
            } | null;
            resources?: {
                [key: string]: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationLocalizationResourceDto"];
            } | null;
            languages?: components["schemas"]["Volo.Abp.Localization.LanguageInfo"][] | null;
            currentCulture?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.CurrentCultureDto"];
            defaultResourceName?: string | null;
            languagesMap?: {
                [key: string]: components["schemas"]["Volo.Abp.NameValue"][];
            } | null;
            languageFilesMap?: {
                [key: string]: components["schemas"]["Volo.Abp.NameValue"][];
            } | null;
            useRouteBasedCulture?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationLocalizationDto": {
            resources?: {
                [key: string]: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationLocalizationResourceDto"];
            } | null;
            currentCulture?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.CurrentCultureDto"];
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationLocalizationResourceDto": {
            texts?: {
                [key: string]: string;
            } | null;
            baseResources?: string[] | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ApplicationSettingConfigurationDto": {
            values?: {
                [key: string]: string | null;
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ClockDto": {
            kind?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.CurrentCultureDto": {
            displayName?: string | null;
            englishName?: string | null;
            threeLetterIsoLanguageName?: string | null;
            twoLetterIsoLanguageName?: string | null;
            isRightToLeft?: boolean;
            cultureName?: string | null;
            name?: string | null;
            nativeName?: string | null;
            dateTimeFormat?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.DateTimeFormatDto"];
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.CurrentUserDto": {
            isAuthenticated?: boolean;
            /** Format: uuid */
            id?: string | null;
            /** Format: uuid */
            tenantId?: string | null;
            /** Format: uuid */
            impersonatorUserId?: string | null;
            /** Format: uuid */
            impersonatorTenantId?: string | null;
            impersonatorUserName?: string | null;
            impersonatorTenantName?: string | null;
            userName?: string | null;
            name?: string | null;
            surName?: string | null;
            email?: string | null;
            emailVerified?: boolean;
            phoneNumber?: string | null;
            phoneNumberVerified?: boolean;
            roles?: string[] | null;
            sessionId?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.DateTimeFormatDto": {
            calendarAlgorithmType?: string | null;
            dateTimeFormatLong?: string | null;
            shortDatePattern?: string | null;
            fullDateTimePattern?: string | null;
            dateSeparator?: string | null;
            shortTimePattern?: string | null;
            longTimePattern?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.IanaTimeZone": {
            timeZoneName?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.EntityExtensionDto": {
            properties?: {
                [key: string]: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyDto"];
            } | null;
            configuration?: {
                [key: string]: unknown;
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionEnumDto": {
            fields?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionEnumFieldDto"][] | null;
            localizationResource?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionEnumFieldDto": {
            name?: string | null;
            value?: unknown;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiCreateDto": {
            isAvailable?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiDto": {
            onGet?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiGetDto"];
            onCreate?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiCreateDto"];
            onUpdate?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiUpdateDto"];
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiGetDto": {
            isAvailable?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiUpdateDto": {
            isAvailable?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyAttributeDto": {
            typeSimple?: string | null;
            config?: {
                [key: string]: unknown;
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyDto": {
            type?: string | null;
            typeSimple?: string | null;
            displayName?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.LocalizableStringDto"];
            api?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyApiDto"];
            ui?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiDto"];
            policy?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyPolicyDto"];
            attributes?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyAttributeDto"][] | null;
            configuration?: {
                [key: string]: unknown;
            } | null;
            defaultValue?: unknown;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyFeaturePolicyDto": {
            features?: string[] | null;
            requiresAll?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyGlobalFeaturePolicyDto": {
            features?: string[] | null;
            requiresAll?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyPermissionPolicyDto": {
            permissionNames?: string[] | null;
            requiresAll?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyPolicyDto": {
            globalFeatures?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyGlobalFeaturePolicyDto"];
            features?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyFeaturePolicyDto"];
            permissions?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyPermissionPolicyDto"];
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiDto": {
            onTable?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiTableDto"];
            onCreateForm?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiFormDto"];
            onEditForm?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiFormDto"];
            lookup?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiLookupDto"];
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiFormDto": {
            isVisible?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiLookupDto": {
            url?: string | null;
            resultListPropertyName?: string | null;
            displayPropertyName?: string | null;
            valuePropertyName?: string | null;
            filterParamName?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionPropertyUiTableDto": {
            isVisible?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.LocalizableStringDto": {
            name?: string | null;
            resource?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ModuleExtensionDto": {
            entities?: {
                [key: string]: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.EntityExtensionDto"];
            } | null;
            configuration?: {
                [key: string]: unknown;
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ObjectExtensionsDto": {
            modules?: {
                [key: string]: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ModuleExtensionDto"];
            } | null;
            enums?: {
                [key: string]: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.ObjectExtending.ExtensionEnumDto"];
            } | null;
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.TimeZone": {
            iana?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.IanaTimeZone"];
            windows?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.WindowsTimeZone"];
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.TimingDto": {
            timeZone?: components["schemas"]["Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.TimeZone"];
        };
        "Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations.WindowsTimeZone": {
            timeZoneId?: string | null;
        };
        "Volo.Abp.AspNetCore.Mvc.MultiTenancy.CurrentTenantDto": {
            /** Format: uuid */
            id?: string | null;
            name?: string | null;
            isAvailable?: boolean;
        };
        "Volo.Abp.AspNetCore.Mvc.MultiTenancy.MultiTenancyInfoDto": {
            isEnabled?: boolean;
            userSharingStrategy?: components["schemas"]["Volo.Abp.MultiTenancy.TenantUserSharingStrategy"];
        };
        "Volo.Abp.Http.Modeling.ActionApiDescriptionModel": {
            uniqueName?: string | null;
            name?: string | null;
            httpMethod?: string | null;
            url?: string | null;
            supportedVersions?: string[] | null;
            parametersOnMethod?: components["schemas"]["Volo.Abp.Http.Modeling.MethodParameterApiDescriptionModel"][] | null;
            parameters?: components["schemas"]["Volo.Abp.Http.Modeling.ParameterApiDescriptionModel"][] | null;
            returnValue?: components["schemas"]["Volo.Abp.Http.Modeling.ReturnValueApiDescriptionModel"];
            allowAnonymous?: boolean | null;
            authorizeDatas?: components["schemas"]["Volo.Abp.Http.Modeling.AuthorizeDataApiDescriptionModel"][] | null;
            implementFrom?: string | null;
            summary?: string | null;
            remarks?: string | null;
            description?: string | null;
            displayName?: string | null;
        };
        "Volo.Abp.Http.Modeling.ApplicationApiDescriptionModel": {
            modules?: {
                [key: string]: components["schemas"]["Volo.Abp.Http.Modeling.ModuleApiDescriptionModel"];
            } | null;
            types?: {
                [key: string]: components["schemas"]["Volo.Abp.Http.Modeling.TypeApiDescriptionModel"];
            } | null;
        };
        "Volo.Abp.Http.Modeling.AuthorizeDataApiDescriptionModel": {
            policy?: string | null;
            roles?: string | null;
        };
        "Volo.Abp.Http.Modeling.ControllerApiDescriptionModel": {
            controllerName?: string | null;
            controllerGroupName?: string | null;
            isRemoteService?: boolean;
            isIntegrationService?: boolean;
            apiVersion?: string | null;
            type?: string | null;
            summary?: string | null;
            remarks?: string | null;
            description?: string | null;
            displayName?: string | null;
            interfaces?: components["schemas"]["Volo.Abp.Http.Modeling.ControllerInterfaceApiDescriptionModel"][] | null;
            actions?: {
                [key: string]: components["schemas"]["Volo.Abp.Http.Modeling.ActionApiDescriptionModel"];
            } | null;
        };
        "Volo.Abp.Http.Modeling.ControllerInterfaceApiDescriptionModel": {
            type?: string | null;
            name?: string | null;
            methods?: components["schemas"]["Volo.Abp.Http.Modeling.InterfaceMethodApiDescriptionModel"][] | null;
        };
        "Volo.Abp.Http.Modeling.InterfaceMethodApiDescriptionModel": {
            name?: string | null;
            parametersOnMethod?: components["schemas"]["Volo.Abp.Http.Modeling.MethodParameterApiDescriptionModel"][] | null;
            returnValue?: components["schemas"]["Volo.Abp.Http.Modeling.ReturnValueApiDescriptionModel"];
        };
        "Volo.Abp.Http.Modeling.MethodParameterApiDescriptionModel": {
            name?: string | null;
            typeAsString?: string | null;
            type?: string | null;
            typeSimple?: string | null;
            isOptional?: boolean;
            defaultValue?: unknown;
            summary?: string | null;
            description?: string | null;
            displayName?: string | null;
        };
        "Volo.Abp.Http.Modeling.ModuleApiDescriptionModel": {
            rootPath?: string | null;
            remoteServiceName?: string | null;
            controllers?: {
                [key: string]: components["schemas"]["Volo.Abp.Http.Modeling.ControllerApiDescriptionModel"];
            } | null;
        };
        "Volo.Abp.Http.Modeling.ParameterApiDescriptionModel": {
            nameOnMethod?: string | null;
            name?: string | null;
            jsonName?: string | null;
            type?: string | null;
            typeSimple?: string | null;
            isOptional?: boolean;
            defaultValue?: unknown;
            constraintTypes?: string[] | null;
            bindingSourceId?: string | null;
            descriptorName?: string | null;
            summary?: string | null;
            description?: string | null;
            displayName?: string | null;
        };
        "Volo.Abp.Http.Modeling.PropertyApiDescriptionModel": {
            name?: string | null;
            jsonName?: string | null;
            type?: string | null;
            typeSimple?: string | null;
            isRequired?: boolean;
            /** Format: int32 */
            minLength?: number | null;
            /** Format: int32 */
            maxLength?: number | null;
            minimum?: string | null;
            maximum?: string | null;
            regex?: string | null;
            isNullable?: boolean;
            summary?: string | null;
            description?: string | null;
            displayName?: string | null;
        };
        "Volo.Abp.Http.Modeling.ReturnValueApiDescriptionModel": {
            type?: string | null;
            typeSimple?: string | null;
            summary?: string | null;
            contentTypes?: string[] | null;
            isRemoteStream?: boolean;
        };
        "Volo.Abp.Http.Modeling.TypeApiDescriptionModel": {
            baseType?: string | null;
            isEnum?: boolean;
            enumNames?: string[] | null;
            enumValues?: unknown[] | null;
            genericArguments?: string[] | null;
            properties?: components["schemas"]["Volo.Abp.Http.Modeling.PropertyApiDescriptionModel"][] | null;
            summary?: string | null;
            remarks?: string | null;
            description?: string | null;
            displayName?: string | null;
        };
        "Volo.Abp.Http.RemoteServiceErrorInfo": {
            code?: string | null;
            message?: string | null;
            details?: string | null;
            data?: {
                [key: string]: unknown;
            } | null;
            validationErrors?: components["schemas"]["Volo.Abp.Http.RemoteServiceValidationErrorInfo"][] | null;
        };
        "Volo.Abp.Http.RemoteServiceErrorResponse": {
            error?: components["schemas"]["Volo.Abp.Http.RemoteServiceErrorInfo"];
        };
        "Volo.Abp.Http.RemoteServiceValidationErrorInfo": {
            message?: string | null;
            members?: string[] | null;
        };
        "Volo.Abp.Localization.LanguageInfo": {
            cultureName?: string | null;
            uiCultureName?: string | null;
            displayName?: string | null;
            readonly twoLetterISOLanguageName?: string | null;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "Volo.Abp.MultiTenancy.TenantUserSharingStrategy": 0 | 1;
        "Volo.Abp.NameValue": {
            name?: string | null;
            value?: string | null;
        };
        "Volo.Abp.SettingManagement.EmailSettingsDto": {
            smtpHost?: string | null;
            /** Format: int32 */
            smtpPort?: number;
            smtpUserName?: string | null;
            smtpPassword?: string | null;
            smtpDomain?: string | null;
            smtpEnableSsl?: boolean;
            smtpUseDefaultCredentials?: boolean;
            defaultFromAddress?: string | null;
            defaultFromDisplayName?: string | null;
        };
        "Volo.Abp.SettingManagement.SendTestEmailInput": {
            senderEmailAddress: string;
            targetEmailAddress: string;
            subject: string;
            body?: string | null;
        };
        "Volo.Abp.SettingManagement.UpdateEmailSettingsDto": {
            smtpHost?: string | null;
            /** Format: int32 */
            smtpPort?: number;
            smtpUserName?: string | null;
            /** Format: password */
            smtpPassword?: string | null;
            smtpDomain?: string | null;
            smtpEnableSsl?: boolean;
            smtpUseDefaultCredentials?: boolean;
            defaultFromAddress: string;
            defaultFromDisplayName: string;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Address.AdministrativeLevel": 1 | 2 | 3;
        "eCommerce.Address.AdministrativeUnitDto": {
            /** Format: uuid */
            id?: string;
            /** Format: int32 */
            schemaVersion?: number;
            level?: components["schemas"]["eCommerce.Address.AdministrativeLevel"];
            code?: string | null;
            name?: string | null;
            parentCode?: string | null;
            isActive?: boolean;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Address.Carrier.CarrierMappingStatus": 0 | 1 | 2;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Address.Carrier.CarrierMatchMethod": 0 | 1 | 2;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Address.Carrier.LoaiSaiLech": 0 | 1 | 2;
        "eCommerce.Address.CarrierAddressMappingDto": {
            /** Format: uuid */
            id?: string;
            carrierCode?: string | null;
            carrierLevel?: string | null;
            carrierAddressCode?: string | null;
            carrierAddressName?: string | null;
            /** Format: uuid */
            ourAdministrativeUnitId?: string;
            matchMethod?: components["schemas"]["eCommerce.Address.Carrier.CarrierMatchMethod"];
            status?: components["schemas"]["eCommerce.Address.Carrier.CarrierMappingStatus"];
            /** Format: date-time */
            syncedAtUtc?: string;
        };
        "eCommerce.Address.ChuanHoaInput": {
            /** Format: int32 */
            schemaVersion?: number;
            tinhCode?: string | null;
            xaCode?: string | null;
            huyenCode?: string | null;
            soNhaDuong?: string | null;
        };
        "eCommerce.Address.DiaChiChoDuyetDto": {
            /** Format: uuid */
            addressId?: string;
            trangThai?: string | null;
            /** Format: int32 */
            schemaVersion?: number;
            diaChiDayDu?: string | null;
            /** Format: date-time */
            creationTime?: string;
        };
        "eCommerce.Address.DiaChiDto": {
            /** Format: uuid */
            addressId?: string;
            trangThai?: string | null;
            /** Format: int32 */
            schemaVersion?: number;
            tinhCode?: string | null;
            tenTinh?: string | null;
            huyenCode?: string | null;
            tenHuyen?: string | null;
            xaCode?: string | null;
            tenXa?: string | null;
            soNhaDuong?: string | null;
            diaChiDayDu?: string | null;
            contentHash?: string | null;
            nguonTao?: string | null;
            /** Format: date-time */
            normalizedAtUtc?: string | null;
            /** Format: date-time */
            xacMinhLucUtc?: string | null;
            /** Format: date-time */
            creationTime?: string;
            coDungDuoc?: boolean;
        };
        "eCommerce.Address.DiaChiSnapshotDto": {
            /** Format: uuid */
            addressId?: string;
            trangThai?: string | null;
            /** Format: int32 */
            schemaVersion?: number;
            tinhCode?: string | null;
            tenTinh?: string | null;
            huyenCode?: string | null;
            tenHuyen?: string | null;
            xaCode?: string | null;
            tenXa?: string | null;
            soNhaDuong?: string | null;
            diaChiDayDu?: string | null;
            /** Format: date-time */
            normalizedAtUtc?: string | null;
            coDungDuoc?: boolean;
        };
        "eCommerce.Address.KetQuaTraMaHangDto": {
            anhXa?: components["schemas"]["eCommerce.Address.CarrierAddressMappingDto"];
            donVi?: components["schemas"]["eCommerce.Address.AdministrativeUnitDto"];
        };
        "eCommerce.Address.MaXaHaiPhienBanDto": {
            /** Format: int32 */
            schemaVersion?: number;
            donVi?: components["schemas"]["eCommerce.Address.AdministrativeUnitDto"];
        };
        "eCommerce.Address.PhatHienSaiLechRequest": {
            carrierCode?: string | null;
            danhMucMoi?: {
                [key: string]: string;
            } | null;
        };
        "eCommerce.Address.SaiLechDto": {
            loai?: components["schemas"]["eCommerce.Address.Carrier.LoaiSaiLech"];
            carrierAddressCode?: string | null;
            moTa?: string | null;
        };
        "eCommerce.Address.TaoDiaChiInput": {
            /** Format: int32 */
            schemaVersion?: number;
            tinhCode?: string | null;
            xaCode?: string | null;
            huyenCode?: string | null;
            soNhaDuong?: string | null;
            diaChiDayDuTho?: string | null;
            nguonTao?: string | null;
        };
        "eCommerce.Address.TraDonViRequest": {
            /** Format: int32 */
            schemaVersion?: number;
            level?: components["schemas"]["eCommerce.Address.AdministrativeLevel"];
            code?: string | null;
        };
        "eCommerce.Address.TraMaHangRequest": {
            carrierCode?: string | null;
            carrierAddressCode?: string | null;
        };
        "eCommerce.Address.XacMinhDiaChiRequest": {
            /** Format: int32 */
            schemaVersion?: number;
            tinhCode?: string | null;
            xaCode?: string | null;
        };
        "eCommerce.Address.XacMinhDiaChiResult": {
            hopLe?: boolean;
            errorCode?: string | null;
            thongDiep?: string | null;
            tinh?: components["schemas"]["eCommerce.Address.AdministrativeUnitDto"];
            xa?: components["schemas"]["eCommerce.Address.AdministrativeUnitDto"];
        };
        "eCommerce.Application.Contracts.Checkout.CheckoutInput": {
            channelCode: string;
            /** Format: uuid */
            channelShopId?: string | null;
            /** Format: uuid */
            customerId?: string | null;
            /** Format: uuid */
            warehouseId: string;
            items: components["schemas"]["eCommerce.Application.Contracts.Checkout.DongCheckoutDto"][];
            /** Format: double */
            orderDiscount?: number;
            /** Format: double */
            orderFee?: number;
            /** Format: int32 */
            ttlPhut?: number | null;
            idempotencyKey: string;
            /** Format: date-time */
            at?: string | null;
        };
        "eCommerce.Application.Contracts.Checkout.DongCheckoutDto": {
            sku: string;
            /** Format: int32 */
            quantity?: number;
            /** Format: double */
            taxRate?: number;
            /** Format: double */
            listPrice?: number | null;
        };
        "eCommerce.Application.Contracts.Checkout.KetQuaCheckoutDto": {
            tien?: components["schemas"]["eCommerce.Pricing.Dtos.GioTinhTienDto"];
            reservationIds?: string[] | null;
            /** Format: date-time */
            expiresAtUtc?: string;
            laKetQuaDaCo?: boolean;
        };
        "eCommerce.Application.Contracts.Inventory.ChinhSachHanMucDto": {
            donVi?: string | null;
            hanhDong?: string | null;
            phamVi?: string | null;
            /** Format: double */
            giaTriMacDinh?: number;
            nguongDoiSoatDonVi?: string | null;
            /** Format: double */
            nguongDoiSoatGiaTri?: number;
            nguongDoiSoatHanhDong?: string | null;
            settingKhongHopLe?: string[] | null;
            ghiChu?: string[] | null;
            /** Format: int32 */
            luuTruDoiSoatSoNgay?: number;
            luuTruDoiSoatHopLe?: boolean;
        };
        "eCommerce.Application.Contracts.Inventory.CreateStockDocumentDto": {
            number: string;
            type?: components["schemas"]["eCommerce.Inventory.StockDocumentType"];
            mode?: components["schemas"]["eCommerce.Inventory.StockDocumentMode"];
            /** Format: uuid */
            warehouseId: string;
            /** Format: uuid */
            toWarehouseId?: string | null;
            /** Format: date-time */
            occurredAtUtc?: string | null;
            note?: string | null;
            counterpartyType?: components["schemas"]["eCommerce.Inventory.StockCounterpartyType"];
            /** Format: uuid */
            counterpartyId?: string | null;
            refType?: string | null;
            refId?: string | null;
            /** Format: double */
            discountAmount?: number | null;
            /** Format: double */
            paidAmount?: number | null;
            idempotencyKey?: string | null;
            lines: components["schemas"]["eCommerce.Application.Contracts.Inventory.CreateStockDocumentLineDto"][];
        };
        "eCommerce.Application.Contracts.Inventory.CreateStockDocumentLineDto": {
            sku: string;
            /** Format: double */
            quantity?: number;
            /** Format: uuid */
            unitId?: string | null;
            /** Format: double */
            vatPercent?: number | null;
            /** Format: uuid */
            batchId?: string | null;
            imeiSerials?: string | null;
            /** Format: double */
            unitPrice?: number | null;
        };
        "eCommerce.Application.Contracts.Inventory.CreateWarehouseDto": {
            code: string;
            name: string;
            address?: string | null;
        };
        "eCommerce.Application.Contracts.Inventory.DatHanMucKenhInput": {
            channelCode: string;
            /** Format: uuid */
            warehouseId?: string | null;
            /** Format: double */
            giaTri?: number;
            hanhDong?: string | null;
            donVi?: string | null;
        };
        "eCommerce.Application.Contracts.Inventory.DoiChinhSachInput": {
            donViHanMuc?: string | null;
            hanhDongHanMuc?: string | null;
            phamViHanMuc?: string | null;
            /** Format: double */
            giaTriMacDinh?: number | null;
            nguongDoiSoatDonVi?: string | null;
            /** Format: double */
            nguongDoiSoatGiaTri?: number | null;
            nguongDoiSoatHanhDong?: string | null;
            /** Format: int32 */
            luuTruDoiSoatSoNgay?: number | null;
        };
        "eCommerce.Application.Contracts.Inventory.GiuChoInput": {
            /** Format: uuid */
            warehouseId: string;
            sku: string;
            /** Format: int32 */
            quantity?: number;
            /** Format: int32 */
            ttlPhut?: number | null;
            referenceType?: string | null;
            referenceId?: string | null;
            channelCode?: string | null;
            /** Format: date-time */
            at?: string | null;
        };
        "eCommerce.Application.Contracts.Inventory.HanMucKenhDto": {
            /** Format: uuid */
            id?: string;
            channelCode?: string | null;
            /** Format: uuid */
            warehouseId?: string | null;
            warehouseCode?: string | null;
            warehouseName?: string | null;
            /** Format: double */
            giaTri?: number;
            hanhDong?: string | null;
            donVi?: string | null;
            readonly dangTat?: boolean;
            /** Format: date-time */
            creationTime?: string;
            /** Format: date-time */
            lastModificationTime?: string | null;
        };
        "eCommerce.Application.Contracts.Inventory.KenhKhaDungDto": {
            code?: string | null;
            name?: string | null;
            holdsInventory?: boolean;
            isActive?: boolean;
            /** Format: int32 */
            soDongHanMuc?: number;
        };
        "eCommerce.Application.Contracts.Inventory.KetQuaChotDto": {
            referenceId?: string | null;
            /** Format: int32 */
            soLuotDaChot?: number;
        };
        "eCommerce.Application.Contracts.Inventory.KetQuaDoiSoatDto": {
            /** Format: int32 */
            soCapDaKiem?: number;
            /** Format: int32 */
            soCapLech?: number;
            cacLech?: components["schemas"]["eCommerce.Application.Contracts.Inventory.LechTonKhoDto"][] | null;
        };
        "eCommerce.Application.Contracts.Inventory.KetQuaNhaChoHetHanDto": {
            /** Format: int32 */
            soLuotDaNha?: number;
        };
        "eCommerce.Application.Contracts.Inventory.KhoKhaDungDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            isActive?: boolean;
            /** Format: int32 */
            soDongHanMuc?: number;
        };
        "eCommerce.Application.Contracts.Inventory.LechTonKhoDto": {
            /** Format: uuid */
            warehouseId?: string;
            sku?: string | null;
            /** Format: double */
            onHand?: number;
            /** Format: double */
            tongSoCai?: number;
            /** Format: double */
            chenhLech?: number;
        };
        "eCommerce.Application.Contracts.Inventory.PostStockDocumentResultDto": {
            /** Format: uuid */
            id?: string;
            number?: string | null;
            status?: string | null;
            /** Format: int32 */
            postedEntries?: number;
        };
        "eCommerce.Application.Contracts.Inventory.StockBalanceDto": {
            /** Format: uuid */
            warehouseId?: string;
            warehouseCode?: string | null;
            warehouseName?: string | null;
            sku?: string | null;
            /** Format: double */
            onHand?: number;
            /** Format: double */
            reserved?: number;
            /** Format: double */
            transferring?: number;
            /** Format: double */
            shipping?: number;
            /** Format: double */
            holding?: number;
            /** Format: double */
            damaged?: number;
            /** Format: double */
            warranty?: number;
            /** Format: double */
            warrantyHolding?: number;
            /** Format: double */
            preOrder?: number;
            /** Format: double */
            waitingInbound?: number;
            /** Format: double */
            inWarehouse?: number;
            /** Format: double */
            available?: number;
        };
        "eCommerce.Application.Contracts.Inventory.StockDocumentDto": {
            /** Format: uuid */
            id?: string;
            number?: string | null;
            type?: string | null;
            status?: string | null;
            /** Format: uuid */
            warehouseId?: string;
            /** Format: uuid */
            toWarehouseId?: string | null;
            /** Format: date-time */
            occurredAtUtc?: string;
            note?: string | null;
            direction?: string | null;
            mode?: string | null;
            counterpartyType?: string | null;
            /** Format: uuid */
            counterpartyId?: string | null;
            refType?: string | null;
            refId?: string | null;
            /** Format: double */
            discountAmount?: number | null;
            /** Format: double */
            paidAmount?: number | null;
            idempotencyKey?: string | null;
            /** Format: int32 */
            lineCount?: number;
            /** Format: double */
            totalAmount?: number | null;
            /** Format: double */
            totalVat?: number | null;
            /** Format: double */
            totalPayment?: number | null;
        };
        "eCommerce.Application.Contracts.Inventory.StockDocumentLineDto": {
            /** Format: uuid */
            id?: string;
            sku?: string | null;
            /** Format: double */
            quantity?: number;
            /** Format: uuid */
            unitId?: string | null;
            /** Format: double */
            vatPercent?: number | null;
            /** Format: uuid */
            batchId?: string | null;
            imeiSerials?: string | null;
            /** Format: double */
            unitPrice?: number | null;
            /** Format: double */
            amount?: number | null;
            /** Format: double */
            vatAmount?: number | null;
        };
        "eCommerce.Application.Contracts.Inventory.StockReservationDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            warehouseId?: string;
            sku?: string | null;
            /** Format: int32 */
            quantity?: number;
            status?: string | null;
            /** Format: date-time */
            expiresAtUtc?: string;
            referenceType?: string | null;
            referenceId?: string | null;
            /** Format: date-time */
            releasedAtUtc?: string | null;
        };
        "eCommerce.Application.Contracts.Inventory.TonKhoDto": {
            /** Format: uuid */
            warehouseId?: string;
            warehouseCode?: string | null;
            warehouseName?: string | null;
            sku?: string | null;
            /** Format: double */
            onHand?: number;
            /** Format: double */
            reserved?: number;
            /** Format: double */
            available?: number;
        };
        "eCommerce.Application.Contracts.Inventory.WarehouseDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            address?: string | null;
            isActive?: boolean;
        };
        "eCommerce.Application.Contracts.Sales.ApproveReturnDto": {
            /** Format: uuid */
            orderId?: string;
            approve?: boolean;
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            reasonText?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.CancelOrderByStatusDto": {
            /** Format: uuid */
            orderId?: string;
            cancelledBy?: components["schemas"]["eCommerce.FulfillmentStatus"];
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            businessReason?: components["schemas"]["eCommerce.OrderReasonCode"];
            reasonText?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.CancelOrderDto": {
            /** Format: uuid */
            orderId?: string;
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            reasonText?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.ChangeFulfillmentStatusDto": {
            /** Format: uuid */
            orderId?: string;
            toStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            reasonText?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.ChangeReconciliationStatusDto": {
            /** Format: uuid */
            orderId?: string;
            toStatus?: components["schemas"]["eCommerce.ReconciliationStatus"];
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            reasonText?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.DonHangDto": {
            /** Format: uuid */
            id?: string;
            number?: string | null;
            channelCode?: string | null;
            status?: string | null;
            currency?: string | null;
            /** Format: double */
            subtotal?: number;
            /** Format: double */
            discountTotal?: number;
            /** Format: double */
            taxTotal?: number;
            /** Format: double */
            shippingTotal?: number;
            /** Format: double */
            grandTotal?: number;
            /** Format: int32 */
            lineCount?: number;
            /** Format: double */
            totalTuSoGiaiTrinh?: number;
            checkoutIdempotencyKey?: string | null;
            fulfillmentStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
            paymentStatus?: components["schemas"]["eCommerce.PaymentStatus"];
            reconciliationStatus?: components["schemas"]["eCommerce.ReconciliationStatus"];
            returnStatus?: components["schemas"]["eCommerce.ReturnStatus"];
            isCancelled?: boolean;
            /** Format: int64 */
            fulfillmentVersion?: number;
            /** Format: int64 */
            paymentVersion?: number;
            /** Format: int64 */
            reconciliationVersion?: number;
            /** Format: int64 */
            closureVersion?: number;
            /** Format: int64 */
            rowVersion?: number;
        };
        "eCommerce.Application.Contracts.Sales.OrderDetailDto": {
            /** Format: uuid */
            id?: string;
            number?: string | null;
            channelCode?: string | null;
            type?: components["schemas"]["eCommerce.OrderType"];
            source?: components["schemas"]["eCommerce.OrderSource"];
            fulfillmentStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
            paymentStatus?: components["schemas"]["eCommerce.PaymentStatus"];
            reconciliationStatus?: components["schemas"]["eCommerce.ReconciliationStatus"];
            returnStatus?: components["schemas"]["eCommerce.ReturnStatus"];
            derivedStatus?: components["schemas"]["eCommerce.OrderStatus"];
            statusLabel?: string | null;
            isCancelled?: boolean;
            /** Format: double */
            grandTotal?: number;
            /** Format: double */
            codAmount?: number | null;
            /** Format: int32 */
            lineCount?: number;
            /** Format: uuid */
            warehouseId?: string | null;
            carrierCode?: string | null;
            trackingNumber?: string | null;
            reasonCode?: components["schemas"]["eCommerce.OrderReasonCode"];
            /** Format: date-time */
            creationTime?: string;
            currency?: string | null;
            /** Format: double */
            subtotal?: number;
            /** Format: double */
            discountTotal?: number;
            /** Format: double */
            taxTotal?: number;
            /** Format: double */
            shippingTotal?: number;
            /** Format: double */
            shippingFeeCustomer?: number | null;
            /** Format: double */
            shippingFeeActual?: number | null;
            /** Format: int32 */
            weightGram?: number | null;
            /** Format: uuid */
            customerId?: string | null;
            /** Format: uuid */
            saleStaffId?: string | null;
            /** Format: uuid */
            packerId?: string | null;
            externalOrderId?: string | null;
            checkoutIdempotencyKey?: string | null;
            note?: string | null;
            /** Format: int64 */
            rowVersion?: number;
            /** Format: int64 */
            fulfillmentVersion?: number;
            /** Format: int64 */
            paymentVersion?: number;
            /** Format: int64 */
            reconciliationVersion?: number;
            /** Format: int64 */
            closureVersion?: number;
            allowedTransitions?: components["schemas"]["eCommerce.FulfillmentStatus"][] | null;
            canCancel?: boolean;
            lines?: components["schemas"]["eCommerce.Application.Contracts.Sales.OrderLineItemDto"][] | null;
        };
        "eCommerce.Application.Contracts.Sales.OrderLineItemDto": {
            /** Format: uuid */
            id?: string;
            sku?: string | null;
            /** Format: uuid */
            productId?: string | null;
            /** Format: int32 */
            quantity?: number;
            /** Format: int32 */
            returnedQuantity?: number;
            /** Format: double */
            unitPrice?: number;
            /** Format: double */
            discountAmount?: number;
            /** Format: double */
            taxAmount?: number;
            /** Format: double */
            lineTotal?: number;
        };
        "eCommerce.Application.Contracts.Sales.OrderListItemDto": {
            /** Format: uuid */
            id?: string;
            number?: string | null;
            channelCode?: string | null;
            type?: components["schemas"]["eCommerce.OrderType"];
            source?: components["schemas"]["eCommerce.OrderSource"];
            fulfillmentStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
            paymentStatus?: components["schemas"]["eCommerce.PaymentStatus"];
            reconciliationStatus?: components["schemas"]["eCommerce.ReconciliationStatus"];
            returnStatus?: components["schemas"]["eCommerce.ReturnStatus"];
            derivedStatus?: components["schemas"]["eCommerce.OrderStatus"];
            statusLabel?: string | null;
            isCancelled?: boolean;
            /** Format: double */
            grandTotal?: number;
            /** Format: double */
            codAmount?: number | null;
            /** Format: int32 */
            lineCount?: number;
            /** Format: uuid */
            warehouseId?: string | null;
            carrierCode?: string | null;
            trackingNumber?: string | null;
            reasonCode?: components["schemas"]["eCommerce.OrderReasonCode"];
            /** Format: date-time */
            creationTime?: string;
        };
        "eCommerce.Application.Contracts.Sales.OrderStatusDto": {
            /** Format: uuid */
            orderId?: string;
            fulfillmentStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
            paymentStatus?: components["schemas"]["eCommerce.PaymentStatus"];
            reconciliationStatus?: components["schemas"]["eCommerce.ReconciliationStatus"];
            returnStatus?: components["schemas"]["eCommerce.ReturnStatus"];
            isCancelled?: boolean;
            derivedStatus?: components["schemas"]["eCommerce.OrderStatus"];
            statusLabel?: string | null;
            stockEffect?: string | null;
            /** Format: int32 */
            stockAffectedLines?: number;
            warning?: string | null;
            /** Format: int64 */
            fulfillmentVersion?: number;
            /** Format: int64 */
            paymentVersion?: number;
            /** Format: int64 */
            reconciliationVersion?: number;
            /** Format: int64 */
            closureVersion?: number;
            /** Format: uuid */
            historyEntryId?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.OrderStatusHistoryDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            orderId?: string;
            axis?: string | null;
            /** Format: int32 */
            fromStatus?: number;
            /** Format: int32 */
            toStatus?: number;
            actorType?: string | null;
            /** Format: uuid */
            actorId?: string | null;
            reasonCode?: string | null;
            reasonText?: string | null;
            correlationId?: string | null;
            /** Format: int64 */
            versionAfter?: number;
            /** Format: date-time */
            occurredAtUtc?: string;
        };
        "eCommerce.Application.Contracts.Sales.OrderTabCountsDto": {
            /** Format: int32 */
            total?: number;
            tabs?: components["schemas"]["eCommerce.Application.Contracts.Sales.OrderTabDto"][] | null;
        };
        "eCommerce.Application.Contracts.Sales.OrderTabDto": {
            key?: string | null;
            label?: string | null;
            /** Format: int32 */
            count?: number;
        };
        "eCommerce.Application.Contracts.Sales.PhanBoKenhDto": {
            channelCode?: string | null;
            sku?: string | null;
            /** Format: double */
            daBan?: number;
            /** Format: double */
            dangGiu?: number;
            /** Format: double */
            daTra?: number;
            dangGiuTheoKho?: {
                [key: string]: number;
            } | null;
        };
        "eCommerce.Application.Contracts.Sales.RequestReturnDto": {
            /** Format: uuid */
            orderId?: string;
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            reasonText?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.TaoDonTuCheckoutInput": {
            checkoutIdempotencyKey: string;
            number: string;
            channelCode: string;
            /** Format: uuid */
            channelShopId?: string | null;
            /** Format: uuid */
            customerId?: string | null;
            note?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.TraHangInput": {
            /** Format: uuid */
            warehouseId: string;
            lines?: components["schemas"]["eCommerce.Application.Contracts.Sales.TraHangLineDto"][] | null;
            /** Format: date-time */
            at?: string | null;
        };
        "eCommerce.Application.Contracts.Sales.TraHangLineDto": {
            sku: string;
            /** Format: int32 */
            quantity?: number;
        };
        "eCommerce.Catalog.Dtos.AddAttributeValueDto": {
            value: string;
            /** Format: int32 */
            sortOrder?: number;
        };
        "eCommerce.Catalog.Dtos.AssignProductDto": {
            /** Format: uuid */
            productId: string;
            isPrimary?: boolean;
            /** Format: int32 */
            sortOrder?: number;
        };
        "eCommerce.Catalog.Dtos.BrandDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            slug?: string | null;
            description?: string | null;
            logoUrl?: string | null;
            isActive?: boolean;
            /** Format: int32 */
            sortOrder?: number;
        };
        "eCommerce.Catalog.Dtos.CatalogAttributeDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            usage?: string | null;
            isActive?: boolean;
            values?: components["schemas"]["eCommerce.Catalog.Dtos.CatalogAttributeValueDto"][] | null;
        };
        "eCommerce.Catalog.Dtos.CatalogAttributeValueDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            attributeId?: string;
            value?: string | null;
            /** Format: int32 */
            sortOrder?: number;
            isActive?: boolean;
        };
        "eCommerce.Catalog.Dtos.CategoryDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            parentId?: string | null;
            code?: string | null;
            name?: string | null;
            slug?: string | null;
            path?: string | null;
            /** Format: int32 */
            level?: number;
            /** Format: int32 */
            sortOrder?: number;
            isActive?: boolean;
            /** Format: int32 */
            maxVariantCombinations?: number | null;
            children?: components["schemas"]["eCommerce.Catalog.Dtos.CategoryDto"][] | null;
        };
        "eCommerce.Catalog.Dtos.CreateBrandDto": {
            code: string;
            name: string;
            slug?: string | null;
            description?: string | null;
            logoUrl?: string | null;
            /** Format: int32 */
            sortOrder?: number;
        };
        "eCommerce.Catalog.Dtos.CreateCatalogAttributeDto": {
            code: string;
            name: string;
            usage?: components["schemas"]["eCommerce.CatalogAttributeUsage"];
        };
        "eCommerce.Catalog.Dtos.CreateCategoryDto": {
            code: string;
            name: string;
            slug?: string | null;
            /** Format: uuid */
            parentId?: string | null;
            /** Format: int32 */
            sortOrder?: number;
            /** Format: int32 */
            maxVariantCombinations?: number | null;
        };
        "eCommerce.Catalog.Dtos.CreateProductDto": {
            code: string;
            name: string;
            slug?: string | null;
            description?: string | null;
            /** Format: uuid */
            brandId?: string | null;
            type?: components["schemas"]["eCommerce.ProductType"];
            /** Format: uuid */
            parentId?: string | null;
            /** Format: double */
            vatPercent?: number | null;
            vatMode?: components["schemas"]["eCommerce.VatMode"];
            /** Format: int32 */
            weightGram?: number | null;
            /** Format: int32 */
            warrantyMonths?: number | null;
            /** Format: int32 */
            storageTimeDays?: number | null;
            isHot?: boolean;
            isNew?: boolean;
            externalId?: string | null;
            units?: components["schemas"]["eCommerce.Catalog.Dtos.CreateProductUnitDto"][] | null;
        };
        "eCommerce.Catalog.Dtos.CreateProductUnitDto": {
            name: string;
            /** Format: double */
            conversionFactor?: number;
            barcode?: string | null;
            isBase?: boolean;
            /** Format: double */
            salePrice?: number;
            /** Format: double */
            importPrice?: number | null;
            /** Format: double */
            wholesalePrice?: number | null;
            /** Format: double */
            oldPrice?: number | null;
        };
        "eCommerce.Catalog.Dtos.CreateVariantDto": {
            sku: string;
            /** Format: double */
            price?: number;
            /** Format: double */
            compareAtPrice?: number | null;
            barcode?: string | null;
            /** Format: int32 */
            weightInGrams?: number | null;
        };
        "eCommerce.Catalog.Dtos.DatHienThiKenhInput": {
            channelCode: string;
            trangThai: string;
            lyDo?: string | null;
        };
        "eCommerce.Catalog.Dtos.GenerateVariantsDto": {
            /** Format: double */
            defaultPrice?: number | null;
            /** Format: int32 */
            maxCombinations?: number | null;
        };
        "eCommerce.Catalog.Dtos.GenerateVariantsResultDto": {
            /** Format: int32 */
            created?: number;
            /** Format: int32 */
            existing?: number;
            /** Format: int32 */
            deactivated?: number;
            /** Format: int32 */
            reactivated?: number;
            skus?: string[] | null;
        };
        "eCommerce.Catalog.Dtos.HienThiHieuLucDto": {
            /** Format: uuid */
            productId?: string;
            trangThai?: string | null;
            coBanGhiTuongMinh?: boolean;
            lyDo?: string | null;
            dangHienThi?: boolean;
        };
        "eCommerce.Catalog.Dtos.HienThiKenhDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            productId?: string;
            channelCode?: string | null;
            tenKenh?: string | null;
            trangThai?: string | null;
            lyDo?: string | null;
            /** Format: date-time */
            creationTime?: string;
            /** Format: date-time */
            lastModificationTime?: string | null;
            kenhConHoatDong?: boolean;
        };
        "eCommerce.Catalog.Dtos.MoveCategoryDto": {
            /** Format: uuid */
            parentId?: string | null;
        };
        "eCommerce.Catalog.Dtos.ProductAttributeSelectionDto": {
            /** Format: uuid */
            attributeId?: string;
            attributeCode?: string | null;
            /** Format: uuid */
            attributeValueId?: string;
            value?: string | null;
        };
        "eCommerce.Catalog.Dtos.ProductDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            slug?: string | null;
            description?: string | null;
            /** Format: uuid */
            brandId?: string | null;
            status?: string | null;
            isPublished?: boolean;
            type?: components["schemas"]["eCommerce.ProductType"];
            /** Format: uuid */
            parentId?: string | null;
            /** Format: double */
            vatPercent?: number | null;
            vatMode?: components["schemas"]["eCommerce.VatMode"];
            /** Format: int32 */
            weightGram?: number | null;
            /** Format: int32 */
            warrantyMonths?: number | null;
            /** Format: int32 */
            storageTimeDays?: number | null;
            isHot?: boolean;
            isNew?: boolean;
            externalId?: string | null;
            units?: components["schemas"]["eCommerce.Catalog.Dtos.ProductUnitDto"][] | null;
            variants?: components["schemas"]["eCommerce.Catalog.Dtos.ProductVariantDto"][] | null;
            /** Format: date-time */
            creationTime?: string;
        };
        "eCommerce.Catalog.Dtos.ProductMediaDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            productId?: string;
            url?: string | null;
            alt?: string | null;
            /** Format: int32 */
            thuTu?: number;
            laAnhChinh?: boolean;
            /** Format: int64 */
            kichThuocByte?: number;
            dinhDang?: string | null;
            contentType?: string | null;
            /** Format: date-time */
            creationTime?: string;
        };
        "eCommerce.Catalog.Dtos.ProductUnitDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            productId?: string;
            name?: string | null;
            /** Format: double */
            conversionFactor?: number;
            barcode?: string | null;
            isBase?: boolean;
            /** Format: double */
            salePrice?: number;
            /** Format: double */
            importPrice?: number | null;
            /** Format: double */
            wholesalePrice?: number | null;
            /** Format: double */
            oldPrice?: number | null;
        };
        "eCommerce.Catalog.Dtos.ProductVariantDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            productId?: string;
            sku?: string | null;
            barcode?: string | null;
            /** Format: double */
            price?: number;
            /** Format: double */
            compareAtPrice?: number | null;
            /** Format: int32 */
            weightInGrams?: number | null;
            isActive?: boolean;
        };
        "eCommerce.Catalog.Dtos.SelectAttributeValueDto": {
            /** Format: uuid */
            attributeValueId: string;
            /** Format: int32 */
            sortOrder?: number;
        };
        "eCommerce.Catalog.Dtos.SetPrimaryCategoryDto": {
            /** Format: uuid */
            categoryId: string;
        };
        "eCommerce.Catalog.Dtos.SuaAnhInput": {
            alt?: string | null;
            /** Format: int32 */
            thuTu?: number;
        };
        "eCommerce.Catalog.Dtos.TaiAnhInput": {
            /** Format: byte */
            noiDung: string;
            alt?: string | null;
            laAnhChinh?: boolean;
        };
        "eCommerce.Catalog.Dtos.TraHieuLucInput": {
            channelCode: string;
            productIds: string[];
            chiHienThi?: boolean;
        };
        "eCommerce.Catalog.Dtos.UpdateBrandDto": {
            name: string;
            slug?: string | null;
            description?: string | null;
            logoUrl?: string | null;
            /** Format: int32 */
            sortOrder?: number;
        };
        "eCommerce.Catalog.Dtos.UpdateCategoryDto": {
            name: string;
            slug?: string | null;
            /** Format: int32 */
            sortOrder?: number;
            /** Format: int32 */
            maxVariantCombinations?: number | null;
        };
        "eCommerce.Catalog.Dtos.UpdateProductDto": {
            name: string;
            slug?: string | null;
            description?: string | null;
            /** Format: uuid */
            brandId?: string | null;
            /** Format: uuid */
            parentId?: string | null;
        };
        "eCommerce.Catalog.Dtos.UpdateVariantPriceDto": {
            /** Format: double */
            price?: number;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.CatalogAttributeUsage": 0 | 1;
        "eCommerce.Channels.Dtos.ChannelShopDto": {
            /** Format: uuid */
            id?: string;
            /** Format: date-time */
            creationTime?: string;
            /** Format: uuid */
            creatorId?: string | null;
            /** Format: date-time */
            lastModificationTime?: string | null;
            /** Format: uuid */
            lastModifierId?: string | null;
            channelCode?: string | null;
            externalShopId?: string | null;
            shopName?: string | null;
            region?: string | null;
            isActive?: boolean;
            hasRefreshToken?: boolean;
            /** Format: date-time */
            accessTokenExpiresAtUtc?: string | null;
            /** Format: date-time */
            authorizationExpiresAtUtc?: string | null;
            /** Format: date-time */
            lastSyncedAtUtc?: string | null;
            pushStatus?: string | null;
            lastError?: string | null;
        };
        "eCommerce.Channels.Dtos.ChannelSyncResultDto": {
            /** Format: uuid */
            channelShopId?: string;
            channelCode?: string | null;
            /** Format: int32 */
            ordersPulled?: number;
            /** Format: int32 */
            listingsPushed?: number;
            /** Format: int32 */
            errors?: number;
            rateLimited?: boolean;
            tokenRefreshed?: boolean;
            biChanDoChuaCoNoiNhanDon?: boolean;
            lyDoChan?: string | null;
            message?: string | null;
            /** Format: date-time */
            startedAtUtc?: string;
            /** Format: date-time */
            finishedAtUtc?: string;
        };
        "eCommerce.Channels.Dtos.ConnectChannelShopDto": {
            channelCode: string;
            externalShopId: string;
            shopName: string;
            region?: string | null;
            accessToken: string;
            refreshToken?: string | null;
            /** Format: date-time */
            accessTokenExpiresAtUtc?: string | null;
            /** Format: date-time */
            refreshTokenExpiresAtUtc?: string | null;
            /** Format: date-time */
            authorizationExpiresAtUtc?: string | null;
        };
        "eCommerce.Customers.CapNhatNhomKhachInput": {
            name: string;
            description?: string | null;
        };
        "eCommerce.Customers.CustomerDto": {
            /** Format: uuid */
            id?: string;
            name?: string | null;
            email?: string | null;
            phone?: string | null;
            /** Format: uuid */
            identityUserId?: string | null;
            status?: string | null;
            dungDuoc?: boolean;
            laKhachVangLai?: boolean;
            /** Format: uuid */
            customerGroupId?: string | null;
            /** Format: double */
            lifetimeValueAmount?: number | null;
            lifetimeValueCurrency?: string | null;
            /** Format: date-time */
            creationTime?: string;
        };
        "eCommerce.Customers.CustomerGroupDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            description?: string | null;
            /** Format: int32 */
            soKhach?: number;
        };
        "eCommerce.Customers.DiaChiCaNhanDto": {
            /** Format: uuid */
            linkId?: string;
            /** Format: uuid */
            addressId?: string;
            addressType?: string | null;
            label?: string | null;
            isDefault?: boolean;
            daGo?: boolean;
            /** Format: date-time */
            lastUsedAtUtc?: string | null;
            diaChiDayDu?: string | null;
            trangThai?: string | null;
            coDungDuoc?: boolean;
            nguon?: string | null;
        };
        "eCommerce.Customers.DiaChiTrongSoDto": {
            /** Format: uuid */
            linkId?: string;
            /** Format: uuid */
            addressId?: string;
            addressType?: string | null;
            label?: string | null;
            isDefault?: boolean;
            /** Format: date-time */
            lastUsedAtUtc?: string | null;
            coDungDuoc?: boolean;
            diaChi?: components["schemas"]["eCommerce.Address.DiaChiSnapshotDto"];
        };
        "eCommerce.Customers.DonHangCaNhanDto": {
            /** Format: uuid */
            orderId?: string;
            soDon?: string | null;
            maKenh?: string | null;
            tienTe?: string | null;
            /** Format: double */
            tongTien?: number;
            trangThai?: string | null;
            trangThaiThanhToan?: string | null;
            /** Format: date-time */
            ngayTaoUtc?: string;
            cacDong?: components["schemas"]["eCommerce.Customers.DongHangCaNhanDto"][] | null;
        };
        "eCommerce.Customers.DongDuLieuCaNhanDto": {
            truong?: string | null;
            giaTri?: string | null;
            nguon?: string | null;
            lyDo?: string | null;
            laDuLieuCaNhan?: boolean;
        };
        "eCommerce.Customers.DongHangCaNhanDto": {
            maSku?: string | null;
            /** Format: int32 */
            soLuong?: number;
            /** Format: double */
            donGia?: number;
            nguon?: string | null;
        };
        "eCommerce.Customers.KetQuaNhatKyXuatDuLieuDto": {
            cacDong?: components["schemas"]["eCommerce.Customers.NhatKyXuatDuLieuDto"][] | null;
            /** Format: int32 */
            tongSo?: number;
            biCat?: boolean;
        };
        "eCommerce.Customers.NangCapKhachInput": {
            /** Format: uuid */
            identityUserId?: string;
        };
        "eCommerce.Customers.NhatKyXuatDuLieuDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            customerId?: string;
            tenKhach?: string | null;
            nguoiXuat?: string | null;
            /** Format: date-time */
            xuatLucUtc?: string;
            /** Format: int32 */
            soDongHoSo?: number;
            /** Format: int32 */
            soDonHang?: number;
            donHangBiCat?: boolean;
            lyDo?: string | null;
        };
        "eCommerce.Customers.SoDiaChiDto": {
            /** Format: uuid */
            customerId?: string;
            cacDiaChi?: components["schemas"]["eCommerce.Customers.DiaChiTrongSoDto"][] | null;
            /** Format: int32 */
            soMacDinhGiaoHang?: number;
            /** Format: int32 */
            soMacDinhHoaDon?: number;
        };
        "eCommerce.Customers.TaoKhachInput": {
            name?: string | null;
            email?: string | null;
            phone?: string | null;
            idempotencyKey?: string | null;
        };
        "eCommerce.Customers.TaoNhomKhachInput": {
            code: string;
            name: string;
            description?: string | null;
        };
        "eCommerce.Customers.ThemDiaChiInput": {
            /** Format: int32 */
            schemaVersion?: number;
            tinhCode?: string | null;
            xaCode?: string | null;
            soNhaDuong?: string | null;
            label?: string | null;
            addressType?: string | null;
            laMacDinh?: boolean;
        };
        "eCommerce.Customers.XuatDuLieuCaNhanDto": {
            /** Format: uuid */
            customerId?: string;
            tenKhach?: string | null;
            /** Format: date-time */
            xuatLucUtc?: string;
            nguoiXuat?: string | null;
            canhBao?: string | null;
            cacDong?: components["schemas"]["eCommerce.Customers.DongDuLieuCaNhanDto"][] | null;
            donHang?: components["schemas"]["eCommerce.Customers.DonHangCaNhanDto"][] | null;
            donHangBiCat?: boolean;
            /** Format: int32 */
            tongSoDon?: number;
            soDiaChi?: components["schemas"]["eCommerce.Customers.DiaChiCaNhanDto"][] | null;
        };
        "eCommerce.EInvoice.Application.DanhDauKetQuaCqtRequest": {
            phanHoiCqt?: string | null;
        };
        "eCommerce.EInvoice.Application.EInvoiceDto": {
            /** Format: uuid */
            id?: string;
            kyHieuMauSo?: string | null;
            soHoaDon?: string | null;
            /** Format: date-time */
            ngayLap?: string;
            status?: components["schemas"]["eCommerce.EInvoice.EInvoiceStatus"];
            taxAuthorityStatus?: components["schemas"]["eCommerce.EInvoice.TaxAuthorityStatus"];
            maCQT?: string | null;
            loiCQT?: string | null;
        };
        "eCommerce.EInvoice.Application.KetQuaGuiCqtGd0Result": {
            /** Format: uuid */
            hoaDonId?: string;
            soHoaDon?: string | null;
            trangThai?: components["schemas"]["eCommerce.EInvoice.EInvoiceStatus"];
            trangThaiCqt?: components["schemas"]["eCommerce.EInvoice.TaxAuthorityStatus"];
            maCQT?: string | null;
            loiCQT?: string | null;
            thanhCong?: boolean;
        };
        "eCommerce.EInvoice.Application.KyHoaDonRequest": {
            xmlDaKy?: string | null;
        };
        "eCommerce.EInvoice.Application.KyHoanTatGd0Request": {
            hashDaKy?: string | null;
            /** Format: uuid */
            chungThuSoId?: string;
            chuKy?: string | null;
            nguoiKy?: string | null;
            xmlDaKy?: string | null;
            /** Format: date-time */
            thoiDiemKy?: string;
        };
        "eCommerce.EInvoice.Application.PhatHanhHoaDonGd0Request": {
            /** Format: int32 */
            schemaVersion?: number;
            tinhCode?: string | null;
            xaCode?: string | null;
            kyHieuMauSo?: string | null;
            /** Format: date-time */
            ngayLap?: string;
        };
        "eCommerce.EInvoice.Application.PhatHanhHoaDonGd0Result": {
            /** Format: uuid */
            hoaDonId?: string;
            soHoaDon?: string | null;
            kyHieuMauSo?: string | null;
            trangThai?: components["schemas"]["eCommerce.EInvoice.EInvoiceStatus"];
            diaChiDaXacMinh?: string | null;
            hashTaiLieu?: string | null;
            /** Format: uuid */
            yeuCauKyId?: string;
            trangThaiKy?: components["schemas"]["eCommerce.Signing.SigningRequestStatus"];
        };
        "eCommerce.EInvoice.Application.TaoHoaDonRequest": {
            kyHieuMauSo?: string | null;
            /** Format: date-time */
            ngayLap?: string;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.EInvoice.EInvoiceStatus": 0 | 1 | 2 | 3 | 4 | 5;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.EInvoice.TaxAuthorityStatus": 0 | 1 | 2 | 3;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.FulfillmentStatus": 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17;
        "eCommerce.HttpApi.Controllers.CancelOrderBody": {
            cancelledBy?: components["schemas"]["eCommerce.FulfillmentStatus"];
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            businessReason?: components["schemas"]["eCommerce.OrderReasonCode"];
            reasonText?: string | null;
        };
        "eCommerce.HttpApi.Controllers.ChangeOrderStatusBody": {
            toStatus?: components["schemas"]["eCommerce.FulfillmentStatus"];
            /** Format: int64 */
            expectedVersion?: number;
            reasonCode?: string | null;
            reasonText?: string | null;
        };
        "eCommerce.HttpApi.Controllers.SystemController+MeResult": {
            /** Format: uuid */
            userId?: string | null;
            userName?: string | null;
            /** Format: uuid */
            tenantId?: string | null;
            permissions?: string[] | null;
            /** Format: int32 */
            permissionCount?: number;
        };
        "eCommerce.HttpApi.Controllers.SystemController+PermissionsResult": {
            granted?: string[] | null;
            /** Format: int32 */
            count?: number;
        };
        "eCommerce.HttpApi.Controllers.SystemController+PingResult": {
            service?: string | null;
            apiVersion?: string | null;
            /** Format: date-time */
            serverTimeUtc?: string;
            authenticated?: boolean;
        };
        "eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockBalanceDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Application.Contracts.Inventory.StockBalanceDto"][] | null;
            /** Format: int64 */
            total?: number;
            /** Format: int32 */
            page?: number;
            /** Format: int32 */
            pageSize?: number;
        };
        "eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.Application.Contracts.Inventory.StockDocumentDto, eCommerce.Inventory.Application.Contracts, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.Application.Contracts.Inventory.StockDocumentDto"][] | null;
            /** Format: int64 */
            total?: number;
            /** Format: int32 */
            page?: number;
            /** Format: int32 */
            pageSize?: number;
        };
        "eCommerce.HttpApi.Dtos.ApiPage`1[[eCommerce.HttpApi.Dtos.ProductListItemDto, eCommerce.HttpApi, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]": {
            items?: components["schemas"]["eCommerce.HttpApi.Dtos.ProductListItemDto"][] | null;
            /** Format: int64 */
            total?: number;
            /** Format: int32 */
            page?: number;
            /** Format: int32 */
            pageSize?: number;
        };
        "eCommerce.HttpApi.Dtos.ProductDetailDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            slug?: string | null;
            status?: string | null;
            isPublished?: boolean;
            sku?: string | null;
            barcode?: string | null;
            /** Format: double */
            price?: number | null;
            /** Format: double */
            compareAtPrice?: number | null;
            /** Format: int32 */
            weightInGrams?: number | null;
            /** Format: int32 */
            variantCount?: number;
            type?: string | null;
            /** Format: double */
            vatPercent?: number | null;
            vatMode?: string | null;
            /** Format: int32 */
            unitCount?: number;
            /** Format: date-time */
            creationTime?: string;
            description?: string | null;
            /** Format: uuid */
            brandId?: string | null;
            /** Format: uuid */
            parentId?: string | null;
            /** Format: int32 */
            weightGram?: number | null;
            /** Format: int32 */
            warrantyMonths?: number | null;
            /** Format: int32 */
            storageTimeDays?: number | null;
            isHot?: boolean;
            isNew?: boolean;
            externalId?: string | null;
            units?: components["schemas"]["eCommerce.HttpApi.Dtos.ProductUnitApiDto"][] | null;
            variants?: components["schemas"]["eCommerce.HttpApi.Dtos.ProductVariantApiDto"][] | null;
        };
        "eCommerce.HttpApi.Dtos.ProductListItemDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            slug?: string | null;
            status?: string | null;
            isPublished?: boolean;
            sku?: string | null;
            barcode?: string | null;
            /** Format: double */
            price?: number | null;
            /** Format: double */
            compareAtPrice?: number | null;
            /** Format: int32 */
            weightInGrams?: number | null;
            /** Format: int32 */
            variantCount?: number;
            type?: string | null;
            /** Format: double */
            vatPercent?: number | null;
            vatMode?: string | null;
            /** Format: int32 */
            unitCount?: number;
            /** Format: date-time */
            creationTime?: string;
        };
        "eCommerce.HttpApi.Dtos.ProductLookupResultDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            sku?: string | null;
            barcode?: string | null;
            /** Format: double */
            price?: number | null;
            status?: string | null;
        };
        "eCommerce.HttpApi.Dtos.ProductUnitApiDto": {
            /** Format: uuid */
            id?: string;
            name?: string | null;
            /** Format: double */
            conversionFactor?: number;
            barcode?: string | null;
            isBase?: boolean;
            /** Format: double */
            salePrice?: number;
            /** Format: double */
            importPrice?: number | null;
            /** Format: double */
            wholesalePrice?: number | null;
            /** Format: double */
            oldPrice?: number | null;
        };
        "eCommerce.HttpApi.Dtos.ProductVariantApiDto": {
            /** Format: uuid */
            id?: string;
            sku?: string | null;
            barcode?: string | null;
            /** Format: double */
            price?: number;
            /** Format: double */
            compareAtPrice?: number | null;
            /** Format: int32 */
            weightInGrams?: number | null;
            isActive?: boolean;
        };
        "eCommerce.Inventory.DonDepDoiSoatInput": {
            /** Format: int32 */
            soNgay?: number | null;
            chiXemTruoc?: boolean;
        };
        "eCommerce.Inventory.DongLechDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            reconRunId?: string;
            /** Format: uuid */
            warehouseId?: string;
            warehouseCode?: string | null;
            sku?: string | null;
            /** Format: double */
            onHand?: number;
            /** Format: double */
            tongSoCai?: number;
            /** Format: double */
            chenhLech?: number;
            vuotNguong?: boolean;
            daXuLy?: boolean;
            /** Format: date-time */
            xuLyLucUtc?: string | null;
            nguoiXuLy?: string | null;
            lyDoXuLy?: string | null;
        };
        "eCommerce.Inventory.Dtos.DatNganSachInput": {
            /** Format: uuid */
            warehouseId?: string;
            sku: string;
            channelCode: string;
            /** Format: int32 */
            soLuong?: number;
            idempotencyKey?: string | null;
            referenceType?: string | null;
            referenceId?: string | null;
        };
        "eCommerce.Inventory.Dtos.LyDoNganSachInput": {
            lyDo: string;
        };
        "eCommerce.Inventory.Dtos.StockAllotmentDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            warehouseId?: string;
            sku?: string | null;
            channelCode?: string | null;
            /** Format: int32 */
            allottedQuantity?: number;
            /** Format: int32 */
            reservedQuantity?: number;
            /** Format: int32 */
            availableToPromise?: number;
            /** Format: int32 */
            priority?: number;
            status?: string | null;
            /** Format: date-time */
            frozenAtUtc?: string | null;
            releaseReason?: string | null;
        };
        "eCommerce.Inventory.KetQuaDonDepDoiSoatDto": {
            /** Format: int32 */
            soNgay?: number;
            /** Format: date-time */
            mocUtc?: string;
            chiXemTruoc?: boolean;
            /** Format: int32 */
            soDongDuDieuKien?: number;
            /** Format: int32 */
            soDongDaXoa?: number;
            /** Format: int32 */
            soDongGiuLaiChuaXuLy?: number;
            /** Format: int32 */
            soDongGiuLaiChoXacNhan?: number;
        };
        "eCommerce.Inventory.LuotDoiSoatDto": {
            /** Format: uuid */
            id?: string;
            /** Format: date-time */
            batDauUtc?: string;
            /** Format: date-time */
            ketThucUtc?: string;
            /** Format: double */
            soGiayQuet?: number;
            /** Format: int32 */
            soCapDaDo?: number;
            /** Format: int32 */
            soLech?: number;
            /** Format: int32 */
            soLechVuotNguong?: number;
            coVuotNguong?: boolean;
            donViNguong?: string | null;
            /** Format: double */
            giaTriNguong?: number;
            hanhDong?: string | null;
            nguongHopLe?: boolean;
            ketLuan?: string | null;
            /** Format: int32 */
            soLechChuaXuLy?: number;
            choXacNhan?: boolean;
            daXacNhan?: boolean;
            /** Format: uuid */
            confirmedByUserId?: string | null;
            confirmedByName?: string | null;
            /** Format: date-time */
            confirmedAtUtc?: string | null;
            confirmNote?: string | null;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Inventory.ReservationStatus": 0 | 1 | 2 | 3;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Inventory.StockCounterpartyType": 1 | 2 | 3 | 4 | 20 | 21;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Inventory.StockDocumentDirection": 1 | 2;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Inventory.StockDocumentMode": 1 | 2 | 3 | 4 | 5 | 6 | 8 | 10 | 13 | 14 | 15 | 16 | 17 | 18 | 19;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Inventory.StockDocumentStatus": 0 | 1 | 2;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Inventory.StockDocumentType": 0 | 1 | 2 | 3 | 4;
        "eCommerce.Inventory.XacNhanLuotInput": {
            ghiChu?: string | null;
        };
        "eCommerce.Inventory.XuLyLechInput": {
            nguoiXuLy?: string | null;
            lyDo?: string | null;
        };
        "eCommerce.Messaging.ChiTietDlqDto": {
            /** Format: uuid */
            id?: string;
            tenSuKien?: string | null;
            khoaNghiepVu?: string | null;
            lyDo?: string | null;
            moTaLyDo?: string | null;
            loi?: string | null;
            /** Format: date-time */
            vaoDlqLucUtc?: string;
            correlationId?: string | null;
            daXuLy?: boolean;
            /** Format: date-time */
            xuLyLucUtc?: string | null;
            nguoiXuLy?: string | null;
            lyDoXuLy?: string | null;
            payloadHash?: string | null;
            payloadJson?: string | null;
        };
        "eCommerce.Messaging.DanhDauXuLyDlqInput": {
            lyDoXuLy: string;
        };
        "eCommerce.Messaging.DlqDto": {
            /** Format: uuid */
            id?: string;
            tenSuKien?: string | null;
            khoaNghiepVu?: string | null;
            lyDo?: string | null;
            moTaLyDo?: string | null;
            loi?: string | null;
            /** Format: date-time */
            vaoDlqLucUtc?: string;
            correlationId?: string | null;
            daXuLy?: boolean;
            /** Format: date-time */
            xuLyLucUtc?: string | null;
            nguoiXuLy?: string | null;
            lyDoXuLy?: string | null;
            payloadHash?: string | null;
        };
        "eCommerce.Messaging.PhatLaiDlqKetQuaDto": {
            /** Format: uuid */
            id?: string;
            tenSuKien?: string | null;
            daPhatLai?: boolean;
            lyDo?: string | null;
            /** Format: date-time */
            phatLaiLucUtc?: string | null;
            /** Format: int32 */
            soLanPhatLai?: number;
            daXuLy?: boolean;
        };
        "eCommerce.Messaging.ThongKeDlqDto": {
            /** Format: int32 */
            soChuaXuLy?: number;
            /** Format: int32 */
            soDaXuLy?: number;
            /** Format: date-time */
            cuNhatChuaXuLyUtc?: string | null;
            theoLyDo?: {
                [key: string]: number;
            } | null;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.OrderReasonCode": 1 | 2 | 3 | 4 | 5 | 6 | 8 | 9 | 10 | 11 | 12 | 14 | 16 | 18 | 19 | 20 | 22 | 23 | 24 | 25 | 26 | 29 | 30 | 35;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.OrderSource": 1 | 2 | 10 | 20 | 21 | 41 | 42 | 43 | 45 | 48 | 49 | 50 | 51 | 52;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.OrderStatus": 0 | 1 | 2 | 3 | 4 | 5 | 6;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.OrderType": 1 | 2 | 3 | 5 | 10 | 12 | 14 | 15 | 16 | 17;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.PaymentDiscrepancyKind": 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.PaymentDiscrepancySeverity": 0 | 1 | 2;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.PaymentDiscrepancyStatus": 0 | 1 | 2 | 3;
        "eCommerce.PaymentHub.HoanTienInput": {
            /** Format: double */
            soTien?: number;
            lyDo?: string | null;
            nguoiThucHien?: string | null;
        };
        "eCommerce.PaymentHub.HoanTienKetQuaDto": {
            /** Format: uuid */
            paymentTransactionId?: string;
            merchantReference?: string | null;
            daNhanYeuCau?: boolean;
            daHoanThat?: boolean;
            choXacNhan?: boolean;
            /** Format: double */
            conHoanDuoc?: number;
            /** Format: double */
            daHoanTong?: number;
            maPhanHoi?: string | null;
            thongDiep?: string | null;
            maGiaoDichHoan?: string | null;
            lyDoThatBai?: string | null;
            loiHaTang?: string | null;
            trangThaiSau?: string | null;
            /** Format: uuid */
            paymentRefundId?: string;
            maYeuCauHoan?: string | null;
            trangThaiDongHoan?: string | null;
        };
        "eCommerce.PaymentHub.KetQuaDoiSoatThanhToanDto": {
            /** Format: int32 */
            soDaXet?: number;
            /** Format: int32 */
            soDoiTrangThai?: number;
            /** Format: int32 */
            soKhongXacDinh?: number;
            /** Format: int32 */
            soThuDuoc?: number;
            /** Format: int32 */
            soLoi?: number;
            maDaDoi?: string[] | null;
        };
        "eCommerce.PaymentHub.PaymentTransactionDto": {
            /** Format: uuid */
            id?: string;
            providerCode?: string | null;
            /** Format: uuid */
            orderId?: string | null;
            merchantReference?: string | null;
            providerTransactionId?: string | null;
            /** Format: double */
            amount?: number;
            /** Format: double */
            refundedAmount?: number;
            /** Format: double */
            conHoanDuoc?: number;
            currency?: string | null;
            status?: string | null;
            /** Format: date-time */
            paidAtUtc?: string | null;
            failureReason?: string | null;
            /** Format: int32 */
            webhookReceivedCount?: number;
            /** Format: date-time */
            creationTime?: string;
            daThuTien?: boolean;
        };
        "eCommerce.PaymentHub.TaoGiaoDichInput": {
            /** Format: uuid */
            orderId?: string | null;
            soDon?: string | null;
            /** Format: double */
            soTien?: number;
            tienTe?: string | null;
            returnUrl?: string | null;
            ipnUrl?: string | null;
            clientIp?: string | null;
            noiDung?: string | null;
        };
        "eCommerce.PaymentHub.TaoGiaoDichKetQuaDto": {
            /** Format: uuid */
            paymentTransactionId?: string;
            merchantReference?: string | null;
            providerCode?: string | null;
            /** Format: double */
            amount?: number;
            currency?: string | null;
            trangThai?: string | null;
            checkoutUrl?: string | null;
            laGiaoDichDaCo?: boolean;
            loiTaoUrl?: string | null;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.PaymentReconciliationLineStatus": 0 | 1 | 2 | 3 | 4 | 5 | 6;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.PaymentReconciliationRunStatus": 0 | 1 | 2 | 3;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.PaymentReconciliationTrigger": 0 | 1;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.PaymentStatus": 0 | 1 | 2 | 3 | 4 | 5;
        "eCommerce.Pricing.Dtos.AddPriceListItemDto": {
            sku: string;
            /** Format: double */
            amount?: number;
        };
        "eCommerce.Pricing.Dtos.AddTierPriceDto": {
            sku: string;
            /** Format: int32 */
            minQuantity?: number;
            /** Format: double */
            amount?: number;
        };
        "eCommerce.Pricing.Dtos.ChiTietDongDto": {
            sku?: string | null;
            /** Format: int32 */
            quantity?: number;
            baseLevel?: string | null;
            baseSource?: string | null;
            /** Format: double */
            unitPriceBeforeRounding?: number;
            winningLevel?: string | null;
            winningSource?: string | null;
            tierApplied?: boolean;
            /** Format: double */
            unitPrice?: number;
            /** Format: double */
            lineSubtotal?: number;
            /** Format: double */
            allocatedDiscount?: number;
            /** Format: double */
            allocatedFee?: number;
            /** Format: double */
            tax?: number;
            /** Format: double */
            lineTotal?: number;
            allCandidates?: components["schemas"]["eCommerce.Pricing.Dtos.ResolvedPriceCandidateDto"][] | null;
        };
        "eCommerce.Pricing.Dtos.CostPriceDto": {
            /** Format: uuid */
            id?: string;
            sku?: string | null;
            /** Format: double */
            amount?: number;
            currencyCode?: string | null;
            /** Format: date */
            effectiveFrom?: string;
            /** Format: date */
            effectiveTo?: string | null;
            /** Format: uuid */
            approvedBy?: string | null;
            /** Format: date-time */
            approvedAtUtc?: string | null;
            daDuyet?: boolean;
        };
        "eCommerce.Pricing.Dtos.CreatePriceListDto": {
            code: string;
            name: string;
            scope?: components["schemas"]["eCommerce.Pricing.PriceListScope"];
            channelCode?: string | null;
            /** Format: uuid */
            channelShopId?: string | null;
            currencyCode?: string | null;
            /** Format: date-time */
            effectiveFrom?: string;
            /** Format: date-time */
            effectiveTo?: string | null;
            /** Format: int32 */
            priority?: number;
        };
        "eCommerce.Pricing.Dtos.CreateTierPriceSetDto": {
            code: string;
            name: string;
            scope?: components["schemas"]["eCommerce.Pricing.PriceListScope"];
            channelCode?: string | null;
            /** Format: uuid */
            channelShopId?: string | null;
            currencyCode?: string | null;
            /** Format: date-time */
            effectiveFrom?: string;
            /** Format: date-time */
            effectiveTo?: string | null;
            /** Format: int32 */
            priority?: number;
        };
        "eCommerce.Pricing.Dtos.DongGioInput": {
            sku: string;
            /** Format: int32 */
            quantity?: number;
            /** Format: double */
            taxRate?: number;
            /** Format: double */
            listPrice?: number | null;
            listPriceCurrency?: string | null;
        };
        "eCommerce.Pricing.Dtos.GioTinhTienDto": {
            currencyCode?: string | null;
            roundingPolicyCode?: string | null;
            /** Format: double */
            subtotal?: number;
            /** Format: double */
            totalDiscount?: number;
            /** Format: double */
            totalFee?: number;
            /** Format: double */
            totalTax?: number;
            /** Format: double */
            total?: number;
            lines?: components["schemas"]["eCommerce.Pricing.Dtos.ChiTietDongDto"][] | null;
        };
        "eCommerce.Pricing.Dtos.PriceCalculationRecordDto": {
            /** Format: uuid */
            id?: string;
            idempotencyKey?: string | null;
            channelCode?: string | null;
            /** Format: uuid */
            channelShopId?: string | null;
            /** Format: date-time */
            calculatedAt?: string;
            roundingPolicyCode?: string | null;
            currencyCode?: string | null;
            /** Format: double */
            subtotal?: number;
            /** Format: double */
            totalDiscount?: number;
            /** Format: double */
            totalFee?: number;
            /** Format: double */
            totalTax?: number;
            /** Format: double */
            total?: number;
            /** Format: date-time */
            creationTime?: string;
        };
        "eCommerce.Pricing.Dtos.PriceListDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            scope?: string | null;
            channelCode?: string | null;
            /** Format: uuid */
            channelShopId?: string | null;
            currencyCode?: string | null;
            /** Format: date-time */
            effectiveFrom?: string;
            /** Format: date-time */
            effectiveTo?: string | null;
            /** Format: int32 */
            priority?: number;
            status?: string | null;
            /** Format: int32 */
            itemCount?: number;
        };
        "eCommerce.Pricing.Dtos.PriceListItemDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            priceListId?: string;
            sku?: string | null;
            /** Format: double */
            amount?: number;
            currencyCode?: string | null;
        };
        "eCommerce.Pricing.Dtos.ResolveManyInput": {
            items: components["schemas"]["eCommerce.Pricing.Dtos.ResolvePriceInput"][];
        };
        "eCommerce.Pricing.Dtos.ResolvePriceInput": {
            sku: string;
            channelCode: string;
            /** Format: uuid */
            channelShopId?: string | null;
            /** Format: int32 */
            quantity?: number;
            /** Format: date-time */
            at?: string | null;
            /** Format: double */
            listPrice?: number | null;
            listPriceCurrency?: string | null;
        };
        "eCommerce.Pricing.Dtos.ResolvedPriceCandidateDto": {
            level?: string | null;
            /** Format: double */
            amount?: number;
            /** Format: int32 */
            priority?: number;
            source?: string | null;
        };
        "eCommerce.Pricing.Dtos.ResolvedPriceDto": {
            sku?: string | null;
            channelCode?: string | null;
            /** Format: double */
            amount?: number;
            currencyCode?: string | null;
            winningLevel?: string | null;
            source?: string | null;
            allCandidates?: components["schemas"]["eCommerce.Pricing.Dtos.ResolvedPriceCandidateDto"][] | null;
        };
        "eCommerce.Pricing.Dtos.TierPriceDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            tierPriceSetId?: string;
            sku?: string | null;
            /** Format: int32 */
            minQuantity?: number;
            /** Format: double */
            amount?: number;
            currencyCode?: string | null;
        };
        "eCommerce.Pricing.Dtos.TierPriceSetDto": {
            /** Format: uuid */
            id?: string;
            code?: string | null;
            name?: string | null;
            scope?: string | null;
            channelCode?: string | null;
            /** Format: uuid */
            channelShopId?: string | null;
            currencyCode?: string | null;
            /** Format: date-time */
            effectiveFrom?: string;
            /** Format: date-time */
            effectiveTo?: string | null;
            /** Format: int32 */
            priority?: number;
            status?: string | null;
            /** Format: int32 */
            tierCount?: number;
        };
        "eCommerce.Pricing.Dtos.TinhGioInput": {
            items: components["schemas"]["eCommerce.Pricing.Dtos.DongGioInput"][];
            channelCode: string;
            /** Format: uuid */
            channelShopId?: string | null;
            /** Format: double */
            orderDiscount?: number;
            /** Format: double */
            orderFee?: number;
            /** Format: date-time */
            at?: string | null;
            idempotencyKey: string;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Pricing.PriceListScope": 0 | 1 | 2;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.ProductStatus": 0 | 1 | 2 | 3;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.ProductType": 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10;
        "eCommerce.Reconciliation.Dtos.PaymentDiscrepancyDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            runId?: string;
            /** Format: uuid */
            lineId?: string;
            /** Format: uuid */
            paymentTransactionId?: string;
            /** Format: uuid */
            orderId?: string | null;
            providerCode?: string | null;
            merchantReference?: string | null;
            kind?: components["schemas"]["eCommerce.PaymentDiscrepancyKind"];
            severity?: components["schemas"]["eCommerce.PaymentDiscrepancySeverity"];
            /** Format: int64 */
            localAmountMinorUnits?: number | null;
            /** Format: int64 */
            providerAmountMinorUnits?: number | null;
            currency?: string | null;
            status?: components["schemas"]["eCommerce.PaymentDiscrepancyStatus"];
            /** Format: date-time */
            detectedAtUtc?: string;
            /** Format: uuid */
            resolvedBy?: string | null;
            /** Format: date-time */
            resolvedAtUtc?: string | null;
            resolutionNote?: string | null;
            ignoreReason?: string | null;
        };
        "eCommerce.Reconciliation.Dtos.PaymentReconciliationLineDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            runId?: string;
            /** Format: uuid */
            paymentTransactionId?: string;
            providerCode?: string | null;
            merchantReference?: string | null;
            requestId?: string | null;
            status?: components["schemas"]["eCommerce.PaymentReconciliationLineStatus"];
            skipReason?: string | null;
            /** Format: int32 */
            attemptCount?: number;
            /** Format: int32 */
            localStatus?: number;
            /** Format: int64 */
            localAmountMinorUnits?: number;
            providerResponseCode?: string | null;
            providerTransactionStatus?: string | null;
            /** Format: int64 */
            providerAmountMinorUnits?: number | null;
            providerTransactionNo?: string | null;
            /** Format: date-time */
            providerPayDateUtc?: string | null;
            failureCode?: string | null;
            failureMessage?: string | null;
            /** Format: uuid */
            discrepancyId?: string | null;
            /** Format: date-time */
            checkedAtUtc?: string | null;
        };
        "eCommerce.Reconciliation.Dtos.PaymentReconciliationRunDto": {
            /** Format: uuid */
            id?: string;
            status?: components["schemas"]["eCommerce.PaymentReconciliationRunStatus"];
            triggeredBy?: components["schemas"]["eCommerce.PaymentReconciliationTrigger"];
            /** Format: uuid */
            triggeredByUserId?: string | null;
            /** Format: date-time */
            periodFromUtc?: string;
            /** Format: date-time */
            periodToUtc?: string;
            /** Format: date-time */
            startedAtUtc?: string;
            /** Format: date-time */
            finishedAtUtc?: string | null;
            /** Format: int32 */
            lineTotal?: number;
            /** Format: int32 */
            lineMatched?: number;
            /** Format: int32 */
            lineSkipped?: number;
            /** Format: int32 */
            discrepancyCount?: number;
            failureCode?: string | null;
            correlationId?: string | null;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.ReconciliationStatus": 0 | 1 | 2 | 3 | 4;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.ReturnStatus": 0 | 1 | 2 | 3 | 4 | 5 | 6;
        "eCommerce.Sales.Dtos.CreateOrderReturnInput": {
            /** Format: uuid */
            orderId?: string;
            /** Format: uuid */
            warehouseId?: string;
            reasonCode: string;
            reasonText?: string | null;
            idempotencyKey: string;
            lines: components["schemas"]["eCommerce.Sales.Dtos.CreateOrderReturnLineInput"][];
        };
        "eCommerce.Sales.Dtos.CreateOrderReturnLineInput": {
            /** Format: uuid */
            orderLineId?: string;
            /** Format: int32 */
            quantity?: number;
        };
        "eCommerce.Sales.Dtos.GhiNhanHoanTienInput": {
            /** Format: double */
            soTien?: number;
        };
        "eCommerce.Sales.Dtos.LyDoTraHangInput": {
            lyDo: string;
        };
        "eCommerce.Sales.Dtos.OrderReturnDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            orderId?: string;
            returnNumber?: string | null;
            channelCode?: string | null;
            status?: string | null;
            goodsStatus?: string | null;
            reasonCode?: string | null;
            reasonText?: string | null;
            /** Format: uuid */
            warehouseId?: string;
            refundMethod?: string | null;
            /** Format: double */
            refundedAmount?: number;
            refundCurrency?: string | null;
            /** Format: int64 */
            version?: number;
            /** Format: date-time */
            receivedAtUtc?: string | null;
            lines?: components["schemas"]["eCommerce.Sales.Dtos.OrderReturnLineDto"][] | null;
        };
        "eCommerce.Sales.Dtos.OrderReturnLineDto": {
            /** Format: uuid */
            id?: string;
            /** Format: uuid */
            orderLineId?: string;
            sku?: string | null;
            /** Format: int32 */
            quantity?: number;
            /** Format: int32 */
            receivedQuantity?: number;
            disposition?: string | null;
            /** Format: double */
            refundAmount?: number;
        };
        "eCommerce.Sales.Dtos.ReceiveOrderReturnInput": {
            lines: components["schemas"]["eCommerce.Sales.Dtos.ReceiveOrderReturnLineInput"][];
        };
        "eCommerce.Sales.Dtos.ReceiveOrderReturnLineInput": {
            /** Format: uuid */
            returnLineId?: string;
            /** Format: int32 */
            receivedQuantity?: number;
            disposition?: string | null;
        };
        "eCommerce.Sales.MarkReconciledInput": {
            channelCode?: string | null;
            externalOrderId?: string | null;
            reason?: string | null;
        };
        "eCommerce.Sales.MarketplaceIntakeDto": {
            channelCode?: string | null;
            externalOrderId?: string | null;
            /** Format: uuid */
            orderId?: string | null;
            /** Format: int64 */
            appliedSourceVersion?: number | null;
            /** Format: date-time */
            appliedSourceUpdatedAtUtc?: string | null;
            appliedPayloadHash?: string | null;
            gapSuspected?: boolean;
            /** Format: date-time */
            lastReceivedAtUtc?: string;
            /** Format: date-time */
            lastReconciledAtUtc?: string | null;
            correlationId?: string | null;
            skipReason?: string | null;
        };
        "eCommerce.Signing.Application.HoanTatKyRequest": {
            hashDaKy?: string | null;
            /** Format: uuid */
            chungThuSoId?: string;
            chuKy?: string | null;
            nguoiKy?: string | null;
            /** Format: date-time */
            thoiDiemKy?: string;
        };
        "eCommerce.Signing.Application.SigningCertificateDto": {
            /** Format: uuid */
            id?: string;
            soSerial?: string | null;
            chuSoHuu?: string | null;
            donViPhatHanh?: string | null;
            /** Format: date-time */
            hieuLucTu?: string;
            /** Format: date-time */
            hieuLucDen?: string;
            trangThai?: components["schemas"]["eCommerce.Signing.CertificateStatus"];
        };
        "eCommerce.Signing.Application.SigningRequestDto": {
            /** Format: uuid */
            id?: string;
            maTaiLieu?: string | null;
            hashTaiLieu?: string | null;
            thuatToanBam?: string | null;
            trangThai?: components["schemas"]["eCommerce.Signing.SigningRequestStatus"];
            nguoiKy?: string | null;
            /** Format: date-time */
            thoiDiemKy?: string | null;
        };
        "eCommerce.Signing.Application.TaoChungThuRequest": {
            soSerial?: string | null;
            chuSoHuu?: string | null;
            donViPhatHanh?: string | null;
            /** Format: date-time */
            hieuLucTu?: string;
            /** Format: date-time */
            hieuLucDen?: string;
        };
        "eCommerce.Signing.Application.TaoYeuCauKyRequest": {
            maTaiLieu?: string | null;
            hashTaiLieu?: string | null;
            thuatToanBam?: string | null;
        };
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Signing.CertificateStatus": 0 | 1;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.Signing.SigningRequestStatus": 0 | 1 | 2;
        /**
         * Format: int32
         * @enum {integer}
         */
        "eCommerce.VatMode": 1 | 2;
    };
    responses: never;
    parameters: never;
    requestBodies: never;
    headers: never;
    pathItems: never;
}
export type $defs = Record<string, never>;
export type operations = Record<string, never>;
