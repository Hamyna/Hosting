// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.AspNet.Http;

namespace Microsoft.AspNet.Hosting
{
    // REVIEW: move to interfaces
    public interface IHttpContextAccessor
    {
        HttpContext HttpContext { get; set; }
    }
}