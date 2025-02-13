﻿using Kudu.Client.Protocol;
using Kudu.Client.Protocol.Master;
using Kudu.Client.Protocol.Tserver;

namespace Kudu.Client.Exceptions
{
    /// <summary>
    /// Representation of an error code and message.
    /// </summary>
    public class KuduStatus
    {
        // Limit the message size we get from the servers as it can be quite large.
        private const int MaxMessageLength = 32 * 1024;

        /// <summary>
        /// 
        /// </summary>
        public AppStatusPB.ErrorCode Code { get; }

        /// <summary>
        /// 
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Get the posix code associated with the error.
        /// -1 if no posix code is set.
        /// </summary>
        public int PosixCode { get; }

        private KuduStatus(AppStatusPB.ErrorCode code, string msg, int posixCode)
        {
            Code = code;
            PosixCode = posixCode;

            if (msg.Length > MaxMessageLength)
            {
                // Truncate the message and indicate that it was abbreviated.
                Message = msg.Substring(0, MaxMessageLength) + "...";
            }
            else
            {
                Message = msg;
            }
        }

        private KuduStatus(AppStatusPB appStatusPB)
            : this(appStatusPB.Code, appStatusPB.Message, appStatusPB.PosixCode)
        {
        }

        private KuduStatus(AppStatusPB.ErrorCode code, string msg)
            : this(code, msg, -1)
        {
        }

        private KuduStatus(AppStatusPB.ErrorCode code)
            : this(code, "", -1)
        {
        }

        /// <summary>
        /// Create a status object from a master error.
        /// </summary>
        /// <param name="masterErrorPB">PB object received via RPC from the master.</param>
        internal static KuduStatus FromMasterErrorPB(MasterErrorPB masterErrorPB)
        {
            return new KuduStatus(masterErrorPB.Status);
        }

        /// <summary>
        /// Create a status object from a tablet server error.
        /// </summary>
        /// <param name="tserverErrorPB">PB object received via RPC from the TS.</param>
        /// <returns></returns>
        internal static KuduStatus FromTabletServerErrorPB(TabletServerErrorPB tserverErrorPB)
        {
            return new KuduStatus(tserverErrorPB.Status);
        }

        /// <summary>
        /// Create a Status object from a {@link WireProtocol.AppStatusPB} protobuf object.
        /// </summary>
        /// <param name="pb"></param>
        internal static KuduStatus FromPB(AppStatusPB pb)
        {
            return new KuduStatus(pb);
        }

        // Keep a single OK status object else we'll end up instantiating tons of them.
        public static KuduStatus Ok { get; } = new KuduStatus(AppStatusPB.ErrorCode.Ok);

        public static KuduStatus NotFound(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.NotFound, msg, posixCode);

        public static KuduStatus Corruption(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.Corruption, msg, posixCode);

        public static KuduStatus NotSupported(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.NotSupported, msg, posixCode);

        public static KuduStatus InvalidArgument(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.InvalidArgument, msg, posixCode);

        public static KuduStatus IOError(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.IoError, msg, posixCode);

        public static KuduStatus AlreadyPresent(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.AlreadyPresent, msg, posixCode);

        public static KuduStatus RuntimeError(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.RuntimeError, msg, posixCode);

        public static KuduStatus NetworkError(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.NetworkError, msg, posixCode);

        public static KuduStatus IllegalState(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.IllegalState, msg, posixCode);

        public static KuduStatus NotAuthorized(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.NotAuthorized, msg, posixCode);

        public static KuduStatus Aborted(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.Aborted, msg, posixCode);

        public static KuduStatus RemoteError(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.RemoteError, msg, posixCode);

        public static KuduStatus ServiceUnavailable(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.ServiceUnavailable, msg, posixCode);

        public static KuduStatus TimedOut(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.TimedOut, msg, posixCode);

        public static KuduStatus Uninitialized(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.Uninitialized, msg, posixCode);

        public static KuduStatus ConfigurationError(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.ConfigurationError, msg, posixCode);

        public static KuduStatus Incomplete(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.Incomplete, msg, posixCode);

        public static KuduStatus EndOfFile(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.EndOfFile, msg, posixCode);

        public static KuduStatus Cancelled(string msg, int posixCode = -1) =>
            new KuduStatus(AppStatusPB.ErrorCode.Cancelled, msg, posixCode);

        public bool IsOk => Code == AppStatusPB.ErrorCode.Ok;

        public bool IsCorruption => Code == AppStatusPB.ErrorCode.Corruption;

        public bool IsNotFound => Code == AppStatusPB.ErrorCode.NotFound;

        public bool IsNotSupported => Code == AppStatusPB.ErrorCode.NotSupported;

        public bool IsInvalidArgument => Code == AppStatusPB.ErrorCode.InvalidArgument;

        public bool IsIOError => Code == AppStatusPB.ErrorCode.IoError;

        public bool IsAlreadyPresent => Code == AppStatusPB.ErrorCode.AlreadyPresent;

        public bool IsRuntimeError => Code == AppStatusPB.ErrorCode.RuntimeError;

        public bool IsNetworkError => Code == AppStatusPB.ErrorCode.NetworkError;

        public bool IsIllegalState => Code == AppStatusPB.ErrorCode.IllegalState;

        public bool IsNotAuthorized => Code == AppStatusPB.ErrorCode.NotAuthorized;

        public bool IsAborted => Code == AppStatusPB.ErrorCode.Aborted;

        public bool IsRemoteError => Code == AppStatusPB.ErrorCode.RemoteError;

        public bool IsServiceUnavailable => Code == AppStatusPB.ErrorCode.ServiceUnavailable;

        public bool IsTimedOut => Code == AppStatusPB.ErrorCode.TimedOut;

        public bool IsUninitialized => Code == AppStatusPB.ErrorCode.Uninitialized;

        public bool IsConfigurationError => Code == AppStatusPB.ErrorCode.ConfigurationError;

        public bool IsIncomplete => Code == AppStatusPB.ErrorCode.Incomplete;

        public bool IsEndOfFile => Code == AppStatusPB.ErrorCode.EndOfFile;

        public bool IsCancelled => Code == AppStatusPB.ErrorCode.Cancelled;

        /// <summary>
        /// Return a human-readable version of the status code.
        /// </summary>
        private string GetCodeAsstring()
        {
            switch (Code) {
                case AppStatusPB.ErrorCode.Ok:
                    return "Ok";
                case AppStatusPB.ErrorCode.NotFound:
                    return "Not found";
                case AppStatusPB.ErrorCode.Corruption:
                    return "Corruption";
                case AppStatusPB.ErrorCode.NotSupported:
                    return "Not implemented";
                case AppStatusPB.ErrorCode.InvalidArgument:
                    return "Invalid argument";
                case AppStatusPB.ErrorCode.IoError:
                    return "IO error";
                case AppStatusPB.ErrorCode.AlreadyPresent:
                    return "Already present";
                case AppStatusPB.ErrorCode.RuntimeError:
                    return "Runtime error";
                case AppStatusPB.ErrorCode.NetworkError:
                    return "Network error";
                case AppStatusPB.ErrorCode.IllegalState:
                    return "Illegal state";
                case AppStatusPB.ErrorCode.NotAuthorized:
                    return "Not authorized";
                case AppStatusPB.ErrorCode.Aborted:
                    return "Aborted";
                case AppStatusPB.ErrorCode.RemoteError:
                    return "Remote error";
                case AppStatusPB.ErrorCode.ServiceUnavailable:
                    return "Service unavailable";
                case AppStatusPB.ErrorCode.TimedOut:
                    return "Timed out";
                case AppStatusPB.ErrorCode.Uninitialized:
                    return "Uninitialized";
                case AppStatusPB.ErrorCode.ConfigurationError:
                    return "Configuration error";
                case AppStatusPB.ErrorCode.Incomplete:
                    return "Incomplete";
                case AppStatusPB.ErrorCode.EndOfFile:
                    return "End of file";
                case AppStatusPB.ErrorCode.Cancelled:
                    return "Cancelled";
                case AppStatusPB.ErrorCode.UnknownError:
                    return "Unknown";
                default:
                    return $"Unknown error ({Code})";
            }
        }

        public override string ToString()
        {
            string str = GetCodeAsstring();
            if (Code == AppStatusPB.ErrorCode.Ok)
            {
                return str;
            }
            str = $"{str}: {Message}";
            if (PosixCode != -1)
            {
                str = $"{str} (error {PosixCode})";
            }
            return str;
        }
    }
}
