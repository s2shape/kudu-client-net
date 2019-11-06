// This file was generated by a tool; you should avoid making direct changes.
// Consider using 'partial classes' to extend these types
// Input: tserver.proto

#pragma warning disable CS1591, CS0612, CS3021, IDE1006
namespace Kudu.Client.Protocol.Tserver
{

    [global::ProtoBuf.ProtoContract()]
    public partial class TabletServerErrorPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, IsRequired = true)]
        public Code code { get; set; } = Code.UnknownError;

        [global::ProtoBuf.ProtoMember(2, Name = @"status", IsRequired = true)]
        public global::Kudu.Client.Protocol.AppStatusPB Status { get; set; }

        [global::ProtoBuf.ProtoContract()]
        public enum Code
        {
            [global::ProtoBuf.ProtoEnum(Name = @"UNKNOWN_ERROR")]
            UnknownError = 1,
            [global::ProtoBuf.ProtoEnum(Name = @"INVALID_SCHEMA")]
            InvalidSchema = 2,
            [global::ProtoBuf.ProtoEnum(Name = @"INVALID_ROW_BLOCK")]
            InvalidRowBlock = 3,
            [global::ProtoBuf.ProtoEnum(Name = @"INVALID_MUTATION")]
            InvalidMutation = 4,
            [global::ProtoBuf.ProtoEnum(Name = @"MISMATCHED_SCHEMA")]
            MismatchedSchema = 5,
            [global::ProtoBuf.ProtoEnum(Name = @"TABLET_NOT_FOUND")]
            TabletNotFound = 6,
            [global::ProtoBuf.ProtoEnum(Name = @"SCANNER_EXPIRED")]
            ScannerExpired = 7,
            [global::ProtoBuf.ProtoEnum(Name = @"INVALID_SCAN_SPEC")]
            InvalidScanSpec = 8,
            [global::ProtoBuf.ProtoEnum(Name = @"INVALID_CONFIG")]
            InvalidConfig = 9,
            [global::ProtoBuf.ProtoEnum(Name = @"TABLET_ALREADY_EXISTS")]
            TabletAlreadyExists = 10,
            [global::ProtoBuf.ProtoEnum(Name = @"TABLET_HAS_A_NEWER_SCHEMA")]
            TabletHasANewerSchema = 11,
            [global::ProtoBuf.ProtoEnum(Name = @"TABLET_NOT_RUNNING")]
            TabletNotRunning = 12,
            [global::ProtoBuf.ProtoEnum(Name = @"INVALID_SNAPSHOT")]
            InvalidSnapshot = 13,
            [global::ProtoBuf.ProtoEnum(Name = @"INVALID_SCAN_CALL_SEQ_ID")]
            InvalidScanCallSeqId = 14,
            [global::ProtoBuf.ProtoEnum(Name = @"NOT_THE_LEADER")]
            NotTheLeader = 15,
            [global::ProtoBuf.ProtoEnum(Name = @"WRONG_SERVER_UUID")]
            WrongServerUuid = 16,
            [global::ProtoBuf.ProtoEnum(Name = @"CAS_FAILED")]
            CasFailed = 17,
            [global::ProtoBuf.ProtoEnum(Name = @"ALREADY_INPROGRESS")]
            AlreadyInprogress = 18,
            [global::ProtoBuf.ProtoEnum(Name = @"THROTTLED")]
            Throttled = 19,
            [global::ProtoBuf.ProtoEnum(Name = @"TABLET_FAILED")]
            TabletFailed = 20,
            [global::ProtoBuf.ProtoEnum(Name = @"NOT_AUTHORIZED")]
            NotAuthorized = 21,
        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PingRequestPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class PingResponsePB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WriteRequestPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"tablet_id", IsRequired = true)]
        public byte[] TabletId { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"schema")]
        public global::Kudu.Client.Protocol.SchemaPB Schema { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"row_operations")]
        public global::Kudu.Client.Protocol.RowOperationsPB RowOperations { get; set; }

        [global::ProtoBuf.ProtoMember(4, Name = @"external_consistency_mode")]
        [global::System.ComponentModel.DefaultValue(global::Kudu.Client.Protocol.ExternalConsistencyModePB.ClientPropagated)]
        public global::Kudu.Client.Protocol.ExternalConsistencyModePB ExternalConsistencyMode
        {
            get { return __pbn__ExternalConsistencyMode ?? global::Kudu.Client.Protocol.ExternalConsistencyModePB.ClientPropagated; }
            set { __pbn__ExternalConsistencyMode = value; }
        }
        public bool ShouldSerializeExternalConsistencyMode() => __pbn__ExternalConsistencyMode != null;
        public void ResetExternalConsistencyMode() => __pbn__ExternalConsistencyMode = null;
        private global::Kudu.Client.Protocol.ExternalConsistencyModePB? __pbn__ExternalConsistencyMode;

        [global::ProtoBuf.ProtoMember(5, Name = @"propagated_timestamp", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
        public ulong PropagatedTimestamp
        {
            get { return __pbn__PropagatedTimestamp.GetValueOrDefault(); }
            set { __pbn__PropagatedTimestamp = value; }
        }
        public bool ShouldSerializePropagatedTimestamp() => __pbn__PropagatedTimestamp != null;
        public void ResetPropagatedTimestamp() => __pbn__PropagatedTimestamp = null;
        private ulong? __pbn__PropagatedTimestamp;

        [global::ProtoBuf.ProtoMember(6, Name = @"authz_token")]
        public global::Kudu.Client.Protocol.Security.SignedTokenPB AuthzToken { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class WriteResponsePB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"error")]
        public TabletServerErrorPB Error { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"per_row_errors")]
        public global::System.Collections.Generic.List<PerRowErrorPB> PerRowErrors { get; } = new global::System.Collections.Generic.List<PerRowErrorPB>();

        [global::ProtoBuf.ProtoMember(3, Name = @"timestamp", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
        public ulong Timestamp
        {
            get { return __pbn__Timestamp.GetValueOrDefault(); }
            set { __pbn__Timestamp = value; }
        }
        public bool ShouldSerializeTimestamp() => __pbn__Timestamp != null;
        public void ResetTimestamp() => __pbn__Timestamp = null;
        private ulong? __pbn__Timestamp;

        [global::ProtoBuf.ProtoContract()]
        public partial class PerRowErrorPB : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"row_index", IsRequired = true)]
            public int RowIndex { get; set; }

            [global::ProtoBuf.ProtoMember(2, Name = @"error", IsRequired = true)]
            public global::Kudu.Client.Protocol.AppStatusPB Error { get; set; }

        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ListTabletsRequestPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"need_schema_info")]
        [global::System.ComponentModel.DefaultValue(true)]
        public bool NeedSchemaInfo
        {
            get { return __pbn__NeedSchemaInfo ?? true; }
            set { __pbn__NeedSchemaInfo = value; }
        }
        public bool ShouldSerializeNeedSchemaInfo() => __pbn__NeedSchemaInfo != null;
        public void ResetNeedSchemaInfo() => __pbn__NeedSchemaInfo = null;
        private bool? __pbn__NeedSchemaInfo;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ListTabletsResponsePB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"error")]
        public TabletServerErrorPB Error { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"status_and_schema")]
        public global::System.Collections.Generic.List<StatusAndSchemaPB> StatusAndSchemas { get; } = new global::System.Collections.Generic.List<StatusAndSchemaPB>();

        [global::ProtoBuf.ProtoContract()]
        public partial class StatusAndSchemaPB : global::ProtoBuf.IExtensible
        {
            private global::ProtoBuf.IExtension __pbn__extensionData;
            global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
                => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

            [global::ProtoBuf.ProtoMember(1, Name = @"tablet_status", IsRequired = true)]
            public global::Kudu.Client.Protocol.Tablet.TabletStatusPB TabletStatus { get; set; }

            [global::ProtoBuf.ProtoMember(2, Name = @"schema")]
            public global::Kudu.Client.Protocol.SchemaPB Schema { get; set; }

            [global::ProtoBuf.ProtoMember(3, Name = @"partition_schema")]
            public global::Kudu.Client.Protocol.PartitionSchemaPB PartitionSchema { get; set; }

        }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ColumnRangePredicatePB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"column", IsRequired = true)]
        public global::Kudu.Client.Protocol.ColumnSchemaPB Column { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"lower_bound")]
        public byte[] LowerBound
        {
            get { return __pbn__LowerBound; }
            set { __pbn__LowerBound = value; }
        }
        public bool ShouldSerializeLowerBound() => __pbn__LowerBound != null;
        public void ResetLowerBound() => __pbn__LowerBound = null;
        private byte[] __pbn__LowerBound;

        [global::ProtoBuf.ProtoMember(3, Name = @"inclusive_upper_bound")]
        public byte[] InclusiveUpperBound
        {
            get { return __pbn__InclusiveUpperBound; }
            set { __pbn__InclusiveUpperBound = value; }
        }
        public bool ShouldSerializeInclusiveUpperBound() => __pbn__InclusiveUpperBound != null;
        public void ResetInclusiveUpperBound() => __pbn__InclusiveUpperBound = null;
        private byte[] __pbn__InclusiveUpperBound;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ColumnRangePredicateListPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"range_predicates")]
        public global::System.Collections.Generic.List<ColumnRangePredicatePB> RangePredicates { get; } = new global::System.Collections.Generic.List<ColumnRangePredicatePB>();

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class NewScanRequestPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"tablet_id", IsRequired = true)]
        public byte[] TabletId { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"limit")]
        public ulong Limit
        {
            get { return __pbn__Limit.GetValueOrDefault(); }
            set { __pbn__Limit = value; }
        }
        public bool ShouldSerializeLimit() => __pbn__Limit != null;
        public void ResetLimit() => __pbn__Limit = null;
        private ulong? __pbn__Limit;

        [global::ProtoBuf.ProtoMember(3, Name = @"DEPRECATED_range_predicates")]
        public global::System.Collections.Generic.List<ColumnRangePredicatePB> DEPRECATEDrangepredicates { get; } = new global::System.Collections.Generic.List<ColumnRangePredicatePB>();

        [global::ProtoBuf.ProtoMember(13, Name = @"column_predicates")]
        public global::System.Collections.Generic.List<global::Kudu.Client.Protocol.ColumnPredicatePB> ColumnPredicates { get; } = new global::System.Collections.Generic.List<global::Kudu.Client.Protocol.ColumnPredicatePB>();

        [global::ProtoBuf.ProtoMember(8, Name = @"start_primary_key")]
        public byte[] StartPrimaryKey
        {
            get { return __pbn__StartPrimaryKey; }
            set { __pbn__StartPrimaryKey = value; }
        }
        public bool ShouldSerializeStartPrimaryKey() => __pbn__StartPrimaryKey != null;
        public void ResetStartPrimaryKey() => __pbn__StartPrimaryKey = null;
        private byte[] __pbn__StartPrimaryKey;

        [global::ProtoBuf.ProtoMember(9, Name = @"stop_primary_key")]
        public byte[] StopPrimaryKey
        {
            get { return __pbn__StopPrimaryKey; }
            set { __pbn__StopPrimaryKey = value; }
        }
        public bool ShouldSerializeStopPrimaryKey() => __pbn__StopPrimaryKey != null;
        public void ResetStopPrimaryKey() => __pbn__StopPrimaryKey = null;
        private byte[] __pbn__StopPrimaryKey;

        [global::ProtoBuf.ProtoMember(4, Name = @"projected_columns")]
        public global::System.Collections.Generic.List<global::Kudu.Client.Protocol.ColumnSchemaPB> ProjectedColumns { get; } = new global::System.Collections.Generic.List<global::Kudu.Client.Protocol.ColumnSchemaPB>();

        [global::ProtoBuf.ProtoMember(5, Name = @"read_mode")]
        [global::System.ComponentModel.DefaultValue(global::Kudu.Client.Protocol.ReadModePB.ReadLatest)]
        public global::Kudu.Client.Protocol.ReadModePB ReadMode
        {
            get { return __pbn__ReadMode ?? global::Kudu.Client.Protocol.ReadModePB.ReadLatest; }
            set { __pbn__ReadMode = value; }
        }
        public bool ShouldSerializeReadMode() => __pbn__ReadMode != null;
        public void ResetReadMode() => __pbn__ReadMode = null;
        private global::Kudu.Client.Protocol.ReadModePB? __pbn__ReadMode;

        [global::ProtoBuf.ProtoMember(16, Name = @"snap_start_timestamp", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
        public ulong SnapStartTimestamp
        {
            get { return __pbn__SnapStartTimestamp.GetValueOrDefault(); }
            set { __pbn__SnapStartTimestamp = value; }
        }
        public bool ShouldSerializeSnapStartTimestamp() => __pbn__SnapStartTimestamp != null;
        public void ResetSnapStartTimestamp() => __pbn__SnapStartTimestamp = null;
        private ulong? __pbn__SnapStartTimestamp;

        [global::ProtoBuf.ProtoMember(6, Name = @"snap_timestamp", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
        public ulong SnapTimestamp
        {
            get { return __pbn__SnapTimestamp.GetValueOrDefault(); }
            set { __pbn__SnapTimestamp = value; }
        }
        public bool ShouldSerializeSnapTimestamp() => __pbn__SnapTimestamp != null;
        public void ResetSnapTimestamp() => __pbn__SnapTimestamp = null;
        private ulong? __pbn__SnapTimestamp;

        [global::ProtoBuf.ProtoMember(7, Name = @"propagated_timestamp", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
        public ulong PropagatedTimestamp
        {
            get { return __pbn__PropagatedTimestamp.GetValueOrDefault(); }
            set { __pbn__PropagatedTimestamp = value; }
        }
        public bool ShouldSerializePropagatedTimestamp() => __pbn__PropagatedTimestamp != null;
        public void ResetPropagatedTimestamp() => __pbn__PropagatedTimestamp = null;
        private ulong? __pbn__PropagatedTimestamp;

        [global::ProtoBuf.ProtoMember(10, Name = @"cache_blocks")]
        [global::System.ComponentModel.DefaultValue(true)]
        public bool CacheBlocks
        {
            get { return __pbn__CacheBlocks ?? true; }
            set { __pbn__CacheBlocks = value; }
        }
        public bool ShouldSerializeCacheBlocks() => __pbn__CacheBlocks != null;
        public void ResetCacheBlocks() => __pbn__CacheBlocks = null;
        private bool? __pbn__CacheBlocks;

        [global::ProtoBuf.ProtoMember(11, Name = @"order_mode")]
        [global::System.ComponentModel.DefaultValue(global::Kudu.Client.Protocol.OrderModePB.Unordered)]
        public global::Kudu.Client.Protocol.OrderModePB OrderMode
        {
            get { return __pbn__OrderMode ?? global::Kudu.Client.Protocol.OrderModePB.Unordered; }
            set { __pbn__OrderMode = value; }
        }
        public bool ShouldSerializeOrderMode() => __pbn__OrderMode != null;
        public void ResetOrderMode() => __pbn__OrderMode = null;
        private global::Kudu.Client.Protocol.OrderModePB? __pbn__OrderMode;

        [global::ProtoBuf.ProtoMember(12, Name = @"last_primary_key")]
        public byte[] LastPrimaryKey
        {
            get { return __pbn__LastPrimaryKey; }
            set { __pbn__LastPrimaryKey = value; }
        }
        public bool ShouldSerializeLastPrimaryKey() => __pbn__LastPrimaryKey != null;
        public void ResetLastPrimaryKey() => __pbn__LastPrimaryKey = null;
        private byte[] __pbn__LastPrimaryKey;

        [global::ProtoBuf.ProtoMember(14, Name = @"row_format_flags")]
        [global::System.ComponentModel.DefaultValue(0)]
        public ulong RowFormatFlags
        {
            get { return __pbn__RowFormatFlags ?? 0; }
            set { __pbn__RowFormatFlags = value; }
        }
        public bool ShouldSerializeRowFormatFlags() => __pbn__RowFormatFlags != null;
        public void ResetRowFormatFlags() => __pbn__RowFormatFlags = null;
        private ulong? __pbn__RowFormatFlags;

        [global::ProtoBuf.ProtoMember(15, Name = @"authz_token")]
        public global::Kudu.Client.Protocol.Security.SignedTokenPB AuthzToken { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ScanRequestPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"scanner_id")]
        public byte[] ScannerId
        {
            get { return __pbn__ScannerId; }
            set { __pbn__ScannerId = value; }
        }
        public bool ShouldSerializeScannerId() => __pbn__ScannerId != null;
        public void ResetScannerId() => __pbn__ScannerId = null;
        private byte[] __pbn__ScannerId;

        [global::ProtoBuf.ProtoMember(2, Name = @"new_scan_request")]
        public NewScanRequestPB NewScanRequest { get; set; }

        [global::ProtoBuf.ProtoMember(3, Name = @"call_seq_id")]
        public uint CallSeqId
        {
            get { return __pbn__CallSeqId.GetValueOrDefault(); }
            set { __pbn__CallSeqId = value; }
        }
        public bool ShouldSerializeCallSeqId() => __pbn__CallSeqId != null;
        public void ResetCallSeqId() => __pbn__CallSeqId = null;
        private uint? __pbn__CallSeqId;

        [global::ProtoBuf.ProtoMember(4, Name = @"batch_size_bytes")]
        public uint BatchSizeBytes
        {
            get { return __pbn__BatchSizeBytes.GetValueOrDefault(); }
            set { __pbn__BatchSizeBytes = value; }
        }
        public bool ShouldSerializeBatchSizeBytes() => __pbn__BatchSizeBytes != null;
        public void ResetBatchSizeBytes() => __pbn__BatchSizeBytes = null;
        private uint? __pbn__BatchSizeBytes;

        [global::ProtoBuf.ProtoMember(5, Name = @"close_scanner")]
        public bool CloseScanner
        {
            get { return __pbn__CloseScanner.GetValueOrDefault(); }
            set { __pbn__CloseScanner = value; }
        }
        public bool ShouldSerializeCloseScanner() => __pbn__CloseScanner != null;
        public void ResetCloseScanner() => __pbn__CloseScanner = null;
        private bool? __pbn__CloseScanner;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ResourceMetricsPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"cfile_cache_miss_bytes")]
        public long CfileCacheMissBytes
        {
            get { return __pbn__CfileCacheMissBytes.GetValueOrDefault(); }
            set { __pbn__CfileCacheMissBytes = value; }
        }
        public bool ShouldSerializeCfileCacheMissBytes() => __pbn__CfileCacheMissBytes != null;
        public void ResetCfileCacheMissBytes() => __pbn__CfileCacheMissBytes = null;
        private long? __pbn__CfileCacheMissBytes;

        [global::ProtoBuf.ProtoMember(2, Name = @"cfile_cache_hit_bytes")]
        public long CfileCacheHitBytes
        {
            get { return __pbn__CfileCacheHitBytes.GetValueOrDefault(); }
            set { __pbn__CfileCacheHitBytes = value; }
        }
        public bool ShouldSerializeCfileCacheHitBytes() => __pbn__CfileCacheHitBytes != null;
        public void ResetCfileCacheHitBytes() => __pbn__CfileCacheHitBytes = null;
        private long? __pbn__CfileCacheHitBytes;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ScanResponsePB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"error")]
        public TabletServerErrorPB Error { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"scanner_id")]
        public byte[] ScannerId
        {
            get { return __pbn__ScannerId; }
            set { __pbn__ScannerId = value; }
        }
        public bool ShouldSerializeScannerId() => __pbn__ScannerId != null;
        public void ResetScannerId() => __pbn__ScannerId = null;
        private byte[] __pbn__ScannerId;

        [global::ProtoBuf.ProtoMember(3, Name = @"has_more_results")]
        public bool HasMoreResults
        {
            get { return __pbn__HasMoreResults.GetValueOrDefault(); }
            set { __pbn__HasMoreResults = value; }
        }
        public bool ShouldSerializeHasMoreResults() => __pbn__HasMoreResults != null;
        public void ResetHasMoreResults() => __pbn__HasMoreResults = null;
        private bool? __pbn__HasMoreResults;

        [global::ProtoBuf.ProtoMember(4, Name = @"data")]
        public global::Kudu.Client.Protocol.RowwiseRowBlockPB Data { get; set; }

        [global::ProtoBuf.ProtoMember(6, Name = @"snap_timestamp", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
        public ulong SnapTimestamp
        {
            get { return __pbn__SnapTimestamp.GetValueOrDefault(); }
            set { __pbn__SnapTimestamp = value; }
        }
        public bool ShouldSerializeSnapTimestamp() => __pbn__SnapTimestamp != null;
        public void ResetSnapTimestamp() => __pbn__SnapTimestamp = null;
        private ulong? __pbn__SnapTimestamp;

        [global::ProtoBuf.ProtoMember(7, Name = @"last_primary_key")]
        public byte[] LastPrimaryKey
        {
            get { return __pbn__LastPrimaryKey; }
            set { __pbn__LastPrimaryKey = value; }
        }
        public bool ShouldSerializeLastPrimaryKey() => __pbn__LastPrimaryKey != null;
        public void ResetLastPrimaryKey() => __pbn__LastPrimaryKey = null;
        private byte[] __pbn__LastPrimaryKey;

        [global::ProtoBuf.ProtoMember(8, Name = @"resource_metrics")]
        public ResourceMetricsPB ResourceMetrics { get; set; }

        [global::ProtoBuf.ProtoMember(9, Name = @"propagated_timestamp", DataFormat = global::ProtoBuf.DataFormat.FixedSize)]
        public ulong PropagatedTimestamp
        {
            get { return __pbn__PropagatedTimestamp.GetValueOrDefault(); }
            set { __pbn__PropagatedTimestamp = value; }
        }
        public bool ShouldSerializePropagatedTimestamp() => __pbn__PropagatedTimestamp != null;
        public void ResetPropagatedTimestamp() => __pbn__PropagatedTimestamp = null;
        private ulong? __pbn__PropagatedTimestamp;

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ScannerKeepAliveRequestPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"scanner_id", IsRequired = true)]
        public byte[] ScannerId { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class ScannerKeepAliveResponsePB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"error")]
        public TabletServerErrorPB Error { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class SplitKeyRangeRequestPB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"tablet_id", IsRequired = true)]
        public byte[] TabletId { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"start_primary_key")]
        public byte[] StartPrimaryKey
        {
            get { return __pbn__StartPrimaryKey; }
            set { __pbn__StartPrimaryKey = value; }
        }
        public bool ShouldSerializeStartPrimaryKey() => __pbn__StartPrimaryKey != null;
        public void ResetStartPrimaryKey() => __pbn__StartPrimaryKey = null;
        private byte[] __pbn__StartPrimaryKey;

        [global::ProtoBuf.ProtoMember(3, Name = @"stop_primary_key")]
        public byte[] StopPrimaryKey
        {
            get { return __pbn__StopPrimaryKey; }
            set { __pbn__StopPrimaryKey = value; }
        }
        public bool ShouldSerializeStopPrimaryKey() => __pbn__StopPrimaryKey != null;
        public void ResetStopPrimaryKey() => __pbn__StopPrimaryKey = null;
        private byte[] __pbn__StopPrimaryKey;

        [global::ProtoBuf.ProtoMember(4, Name = @"target_chunk_size_bytes")]
        public ulong TargetChunkSizeBytes
        {
            get { return __pbn__TargetChunkSizeBytes.GetValueOrDefault(); }
            set { __pbn__TargetChunkSizeBytes = value; }
        }
        public bool ShouldSerializeTargetChunkSizeBytes() => __pbn__TargetChunkSizeBytes != null;
        public void ResetTargetChunkSizeBytes() => __pbn__TargetChunkSizeBytes = null;
        private ulong? __pbn__TargetChunkSizeBytes;

        [global::ProtoBuf.ProtoMember(5, Name = @"columns")]
        public global::System.Collections.Generic.List<global::Kudu.Client.Protocol.ColumnSchemaPB> Columns { get; } = new global::System.Collections.Generic.List<global::Kudu.Client.Protocol.ColumnSchemaPB>();

        [global::ProtoBuf.ProtoMember(6, Name = @"authz_token")]
        public global::Kudu.Client.Protocol.Security.SignedTokenPB AuthzToken { get; set; }

    }

    [global::ProtoBuf.ProtoContract()]
    public partial class SplitKeyRangeResponsePB : global::ProtoBuf.IExtensible
    {
        private global::ProtoBuf.IExtension __pbn__extensionData;
        global::ProtoBuf.IExtension global::ProtoBuf.IExtensible.GetExtensionObject(bool createIfMissing)
            => global::ProtoBuf.Extensible.GetExtensionObject(ref __pbn__extensionData, createIfMissing);

        [global::ProtoBuf.ProtoMember(1, Name = @"error")]
        public TabletServerErrorPB Error { get; set; }

        [global::ProtoBuf.ProtoMember(2, Name = @"ranges")]
        public global::System.Collections.Generic.List<global::Kudu.Client.Protocol.KeyRangePB> Ranges { get; } = new global::System.Collections.Generic.List<global::Kudu.Client.Protocol.KeyRangePB>();

    }

    [global::ProtoBuf.ProtoContract()]
    public enum RowFormatFlags
    {
        [global::ProtoBuf.ProtoEnum(Name = @"NO_FLAGS")]
        NoFlags = 0,
        [global::ProtoBuf.ProtoEnum(Name = @"PAD_UNIX_TIME_MICROS_TO_16_BYTES")]
        PadUnixTimeMicrosTo16Bytes = 1,
    }

    [global::ProtoBuf.ProtoContract()]
    public enum TabletServerFeatures
    {
        [global::ProtoBuf.ProtoEnum(Name = @"UNKNOWN_FEATURE")]
        UnknownFeature = 0,
        [global::ProtoBuf.ProtoEnum(Name = @"COLUMN_PREDICATES")]
        ColumnPredicates = 1,
        [global::ProtoBuf.ProtoEnum(Name = @"PAD_UNIXTIME_MICROS_TO_16_BYTES")]
        PadUnixtimeMicrosTo16Bytes = 2,
    }

}

#pragma warning restore CS1591, CS0612, CS3021, IDE1006
