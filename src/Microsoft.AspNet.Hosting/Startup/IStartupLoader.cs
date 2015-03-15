// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.AspNet.Hosting.Startup
{
    // REVIEW: Kill interface? not replaceable anymore
    public interface IStartupLoader
    {
        ApplicationStartup LoadStartup(
            string applicationName, 
            string environmentName,
            IList<string> diagnosticMessages);
    }
}
