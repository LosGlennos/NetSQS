using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Amazon;

namespace NetSQS
{
    public static class Regions
    {
        public static RegionEndpoint GetEndpoint(string systemName)
        {
            return RegionEndpoint.EnumerableAllRegions.Single(x => x.SystemName == systemName);
        }
    }
}
