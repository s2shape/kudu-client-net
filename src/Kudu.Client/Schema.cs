﻿using System;
using System.Collections.Generic;
using Kudu.Client.Protocol;

namespace Kudu.Client
{
    /// <summary>
    /// Represents table's schema which is essentially a list of columns.
    /// </summary>
    public class Schema
    {
        /// <summary>
        /// Maps column index to column.
        /// </summary>
        private readonly ColumnSchema[] _columnsByIndex;

        /// <summary>
        /// The primary key columns.
        /// </summary>
        private readonly List<ColumnSchema> _primaryKeyColumns;

        /// <summary>
        /// Maps column name to column index.
        /// </summary>
        private readonly Dictionary<string, int> _columnsByName;

        /// <summary>
        /// Maps columnId to column index.
        /// </summary>
        private readonly Dictionary<int, int> _columnsById;

        /// <summary>
        /// Maps column index to data index.
        /// </summary>
        private readonly int[] _columnOffsets;

        /// <summary>
        /// The size of all fixed-length columns.
        /// </summary>
        public int RowAllocSize { get; }

        /// <summary>
        /// Get the size a row built using this schema would be
        /// </summary>
        public int RowSize { get; }

        public int VarLengthColumnCount { get; }

        public bool HasNullableColumns { get; }

        public Schema(List<ColumnSchema> columns)
            : this(columns, null) { }

        public Schema(List<ColumnSchema> columns, List<int> columnIds)
        {
            var hasColumnIds = columnIds != null;
            if (hasColumnIds)
                _columnsById = new Dictionary<int, int>(columns.Count);

            _primaryKeyColumns = new List<ColumnSchema>();
            _columnsByName = new Dictionary<string, int>(columns.Count);
            _columnsById = new Dictionary<int, int>(columns.Count);
            _columnOffsets = new int[columns.Count];

            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];

                if (column.Type == KuduType.String || column.Type == KuduType.Binary)
                {
                    _columnOffsets[i] = VarLengthColumnCount;
                    VarLengthColumnCount++;

                    // Don't increment size here, these types are stored separately
                    // in PartialRow (_varLengthData).
                }
                else
                {
                    _columnOffsets[i] = RowAllocSize;
                    RowAllocSize += column.Size;
                }

                HasNullableColumns |= column.IsNullable;
                _columnsByName.Add(column.Name, i);

                if (hasColumnIds)
                    _columnsById.Add(columnIds[i], i);

                if (column.IsKey)
                    _primaryKeyColumns.Add(column);
            }

            _columnsByIndex = new ColumnSchema[columns.Count];
            columns.CopyTo(_columnsByIndex);
            RowSize = GetRowSize(_columnsByIndex);
        }

        public Schema(SchemaPB schema)
        {
            var columns = schema.Columns;

            var size = 0;
            var varLenCnt = 0;
            var hasNulls = false;
            var primaryKeyColumns = new List<ColumnSchema>();
            var columnsByName = new Dictionary<string, int>(columns.Count);
            var columnsById = new Dictionary<int, int>(columns.Count);
            var columnOffsets = new int[columns.Count];
            var columnsByIndex = new ColumnSchema[columns.Count];

            for (int i = 0; i < columns.Count; i++)
            {
                var column = columns[i];

                if (column.Type == DataTypePB.String || column.Type == DataTypePB.Binary)
                {
                    columnOffsets[i] = varLenCnt;
                    varLenCnt++;
                    // Don't increment size here, these types are stored separately
                    // in PartialRow.
                }
                else
                {
                    columnOffsets[i] = size;
                    size += GetTypeSize((KuduType)column.Type);
                }

                hasNulls |= column.IsNullable;
                columnsByName.Add(column.Name, i);
                columnsById.Add((int)column.Id, i);
                columnsByIndex[i] = ColumnSchema.FromProtobuf(columns[i]);

                if (column.IsKey)
                    primaryKeyColumns.Add(columnsByIndex[i]);

                // TODO: Remove this hack-fix. Kudu throws an exception if columnId is supplied.
                column.ResetId();
            }

            _columnOffsets = columnOffsets;
            _columnsByName = columnsByName;
            _columnsById = columnsById;
            _columnsByIndex = columnsByIndex;
            _primaryKeyColumns = primaryKeyColumns;
            RowAllocSize = size;
            VarLengthColumnCount = varLenCnt;
            HasNullableColumns = hasNulls;
            RowSize = GetRowSize(columnsByIndex);
        }

        public IReadOnlyList<ColumnSchema> Columns => _columnsByIndex;

        public int PrimaryKeyColumnCount => _primaryKeyColumns.Count;

        public int GetColumnIndex(string name) => _columnsByName[name];

        /// <summary>
        /// If the column is a fixed-length type, the offset is where that
        /// column should be in <see cref="PartialRow._rowAlloc"/>. If the
        /// column is variable-length, the offset is where that column should
        /// be stored in <see cref="PartialRow._varLengthData"/>.
        /// </summary>
        /// <param name="index">The column index.</param>
        public int GetColumnOffset(int index) => _columnOffsets[index];

        public ColumnSchema GetColumn(int index) => _columnsByIndex[index];

        public ColumnSchema GetColumn(string name) => GetColumn(GetColumnIndex(name));

        public int GetColumnIndex(int id) => _columnsById[id];

        /// <summary>
        /// Returns true if the column exists.
        /// </summary>
        /// <param name="columnName">Column to search for.</param>
        public bool HasColumn(string columnName) => _columnsByName.ContainsKey(columnName);

        public static int GetTypeSize(KuduType type)
        {
            switch (type)
            {
                case KuduType.String:
                case KuduType.Binary:
                    return 8 + 8; // Offset then string length.
                case KuduType.Bool:
                case KuduType.Int8:
                    return 1;
                case KuduType.Int16:
                    return 2;
                case KuduType.Int32:
                case KuduType.Float:
                case KuduType.Decimal32:
                    return 4;
                case KuduType.Int64:
                case KuduType.Double:
                case KuduType.UnixtimeMicros:
                case KuduType.Decimal64:
                    return 8;
                //case DataType.Int128: Not supported in Kudu yet.
                case KuduType.Decimal128:
                    return 16;
                default:
                    throw new ArgumentException();
            }
        }

        public static bool IsSigned(KuduType type)
        {
            switch (type)
            {
                case KuduType.Int8:
                case KuduType.Int16:
                case KuduType.Int32:
                case KuduType.Int64:
                    //case DataType.Int128: Not supported in Kudu yet.
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gives the size in bytes for a single row given the specified schema
        /// </summary>
        /// <param name="columns">The row's columns.</param>
        private static int GetRowSize(ColumnSchema[] columns)
        {
            int totalSize = 0;
            bool hasNullables = false;
            foreach (ColumnSchema column in columns)
            {
                totalSize += column.Size;
                hasNullables |= column.IsNullable;
            }
            if (hasNullables)
            {
                totalSize += BitsToBytes(columns.Length);
            }
            return totalSize;
        }

        // TODO: Move this to shared location.
        private static int BitsToBytes(int bits) => (bits + 7) / 8;
    }
}
