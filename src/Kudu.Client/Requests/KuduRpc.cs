﻿using Kudu.Client.Connection;
using Kudu.Client.Protocol.Master;

namespace Kudu.Client.Requests
{
    public abstract class KuduRpc<TRequest, TResponse>
    {
        // Service names.
        protected const string MasterServiceName = "kudu.master.MasterService";
        protected const string TabletServerServiceName = "kudu.tserver.TabletServerService";

        public abstract string ServiceName { get; }

        public abstract string MethodName { get; }

        public abstract TRequest Request { get; }

        public virtual TResponse ParseResponse(CallResponse response)
        {
            using (response)
            {
                return response.ParseResponse<TResponse>();
            }
        }

        internal protected virtual byte[] PartitionKey => null;

        internal protected virtual MasterFeatures[] MasterFeatures => null;
    }
}
