﻿using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Client.Protocol;
using Kudu.Client.Util;

namespace Kudu.Client
{
    /// <summary>
    /// A predicate which can be used to filter rows based on the value of a column.
    /// </summary>
    public class KuduPredicate : IEquatable<KuduPredicate>
    {
        /// <summary>
        /// The inclusive lower bound value if this is a Range predicate, or
        /// the createEquality value if this is an Equality predicate.
        /// </summary>
        internal byte[] Lower { get; }

        /// <summary>
        /// The exclusive upper bound value if this is a Range predicate.
        /// </summary>
        internal byte[] Upper { get; }

        /// <summary>
        /// In-list values.
        /// </summary>
        internal SortedSet<byte[]> InListValues { get; }

        public PredicateType Type { get; }

        public ColumnSchema Column { get; }

        /// <summary>
        /// Constructor for all non IN list predicates.
        /// </summary>
        /// <param name="type">The predicate type.</param>
        /// <param name="column">The column to which the predicate applies.</param>
        /// <param name="lower">
        /// The lower bound serialized value if this is a Range predicate,
        /// or the equality value if this is an Equality predicate.
        /// </param>
        /// <param name="upper">The upper bound serialized value if this is a Range predicate.</param>
        public KuduPredicate(PredicateType type, ColumnSchema column, byte[] lower, byte[] upper)
        {
            Type = type;
            Column = column;
            Lower = lower;
            Upper = upper;
        }

        /// <summary>
        /// Constructor for IN list predicate.
        /// </summary>
        /// <param name="column">The column to which the predicate applies.</param>
        /// <param name="inListValues">The encoded IN list values.</param>
        public KuduPredicate(ColumnSchema column, SortedSet<byte[]> inListValues)
        {
            Column = column;
            Type = PredicateType.InList;
            InListValues = inListValues;
        }

        public bool Equals(KuduPredicate other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            return Type == other.Type &&
                Column.Equals(other.Column) &&
                Lower.AsSpan().SequenceEqual(other.Lower) &&
                Upper.AsSpan().SequenceEqual(other.Upper) &&
                InListEquals(other.InListValues);
        }

        private bool InListEquals(SortedSet<byte[]> other)
        {
            if (InListValues is null && other is null)
                return true;

            if (InListValues is null || other is null)
                return false;

            return InListValues.SetEquals(other);
        }

        public override bool Equals(object obj) => Equals(obj as KuduPredicate);

        public override int GetHashCode() => HashCode.Combine(Type, Column);

        /// <summary>
        /// Merges another <see cref="KuduPredicate"/> into this one, returning a new
        /// <see cref="KuduPredicate"/> which matches the logical intersection (AND)
        /// of the input predicates.
        /// </summary>
        /// <param name="other">The predicate to merge with this predicate.</param>
        public KuduPredicate Merge(KuduPredicate other)
        {
            if (!Column.Equals(other.Column))
                throw new ArgumentException("Predicates from different columns may not be merged");

            // First, consider other.type == NONE, IS_NOT_NULL, or IS_NULL
            // NONE predicates dominate.
            if (other.Type == PredicateType.None)
                return other;

            // NOT NULL is dominated by all other predicates,
            // except IS NULL, for which the merge is NONE.
            if (other.Type == PredicateType.IsNotNull)
                return Type == PredicateType.IsNull ? None(Column) : this;

            // NULL merged with any predicate type besides itself is NONE.
            if (other.Type == PredicateType.IsNull)
                return Type == PredicateType.IsNull ? this : None(Column);

            // Now other.type == EQUALITY, RANGE, or IN_LIST.
            switch (Type)
            {
                case PredicateType.None: return this;
                case PredicateType.IsNotNull: return other;
                case PredicateType.IsNull: return None(Column);
                case PredicateType.Equality:
                    {
                        if (other.Type == PredicateType.Equality)
                        {
                            if (Compare(Column, Lower, other.Lower) != 0)
                            {
                                return None(Column);
                            }
                            else
                            {
                                return this;
                            }
                        }
                        else if (other.Type == PredicateType.Range)
                        {
                            if (other.RangeContains(Lower))
                            {
                                return this;
                            }
                            else
                            {
                                return None(Column);
                            }
                        }
                        else
                        {
                            //Preconditions.checkState(other.type == PredicateType.IN_LIST);
                            return other.Merge(this);
                        }
                    }
                case PredicateType.Range:
                    {
                        if (other.Type == PredicateType.Equality || other.Type == PredicateType.InList)
                        {
                            return other.Merge(this);
                        }
                        else
                        {
                            //Preconditions.checkState(other.type == PredicateType.RANGE);
                            byte[] newLower = other.Lower == null ||
                                (Lower != null && Compare(Column, Lower, other.Lower) >= 0) ? Lower : other.Lower;
                            byte[] newUpper = other.Upper == null ||
                                (Upper != null && Compare(Column, Upper, other.Upper) <= 0) ? Upper : other.Upper;
                            if (newLower != null && newUpper != null && Compare(Column, newLower, newUpper) >= 0)
                            {
                                return None(Column);
                            }
                            else
                            {
                                if (newLower != null && newUpper != null && AreConsecutive(newLower, newUpper))
                                {
                                    return new KuduPredicate(PredicateType.Equality, Column, newLower, null);
                                }
                                else
                                {
                                    return new KuduPredicate(PredicateType.Range, Column, newLower, newUpper);
                                }
                            }
                        }
                    }
                case PredicateType.InList:
                    {
                        if (other.Type == PredicateType.Equality)
                        {
                            if (InListValues.Contains(other.Lower))
                            {
                                return other;
                            }
                            else
                            {
                                return None(Column);
                            }
                        }
                        else if (other.Type == PredicateType.Range)
                        {
                            var comparer = new PredicateComparer(Column);
                            var values = new SortedSet<byte[]>(comparer);
                            foreach (var value in InListValues)
                            {
                                if (other.RangeContains(value))
                                {
                                    values.Add(value);
                                }
                            }
                            return BuildInList(Column, values);
                        }
                        else
                        {
                            //Preconditions.checkState(other.type == PredicateType.IN_LIST);
                            var comparer = new PredicateComparer(Column);
                            var values = new SortedSet<byte[]>(comparer);
                            foreach (var value in InListValues)
                            {
                                if (other.InListValues.Contains(value))
                                {
                                    values.Add(value);
                                }
                            }
                            return BuildInList(Column, values);
                        }
                    }
                default:
                    throw new Exception($"Unknown predicate type {Type}");
            }
        }

        public ColumnPredicatePB ToProtobuf()
        {
            var predicate = new ColumnPredicatePB { Column = Column.Name };

            switch (Type)
            {
                case PredicateType.Equality:
                    predicate.equality = new ColumnPredicatePB.Equality { Value = Lower };
                    break;

                case PredicateType.Range:
                    predicate.range = new ColumnPredicatePB.Range
                    {
                        Lower = Lower,
                        Upper = Upper
                    };
                    break;

                case PredicateType.IsNotNull:
                    predicate.is_not_null = new ColumnPredicatePB.IsNotNull();
                    break;

                case PredicateType.IsNull:
                    predicate.is_null = new ColumnPredicatePB.IsNull();
                    break;

                case PredicateType.InList:
                    predicate.in_list = new ColumnPredicatePB.InList();
                    predicate.in_list.Values.AddRange(InListValues);
                    break;

                case PredicateType.None:
                    throw new Exception("Can not convert None predicate to protobuf message");

                default:
                    throw new Exception($"Unknown predicate type {Type}");
            }

            return predicate;
        }

        public override string ToString()
        {
            var name = Column.Name;

            switch (Type)
            {
                case PredicateType.Equality: return $"`{name}` = {ValueToString(Lower)}";
                case PredicateType.Range:
                    {
                        if (Lower is null)
                        {
                            return $"`{name}` < {ValueToString(Upper)}";
                        }
                        else if (Upper is null)
                        {
                            return $"`{name}` >= {ValueToString(Lower)}";
                        }
                        else
                        {
                            return $"`{name}` >= {ValueToString(Lower)} AND `{name}` < {ValueToString(Upper)}";
                        }
                    }
                case PredicateType.InList:
                    {
                        var strings = new List<string>(InListValues.Count);
                        foreach (var value in InListValues)
                            strings.Add(ValueToString(value));
                        return $"`{name}` IN ({string.Join(", ", strings)})";
                    }
                case PredicateType.IsNotNull: return $"`{name}` IS NOT NULL";
                case PredicateType.IsNull: return $"`{name}` IS NULL";
                case PredicateType.None: return $"`{name}` NONE";
                default: throw new Exception($"Unknown predicate type {Type}");
            }
        }

        /// <summary>
        /// Returns the string value of serialized value according to the type of column.
        /// </summary>
        /// <param name="value">The value.</param>
        private string ValueToString(byte[] value)
        {
            switch (Column.Type) {
                case KuduType.Bool:
                    return KuduEncoder.DecodeBool(value).ToString();
                case KuduType.Int8:
                    return KuduEncoder.DecodeInt8(value).ToString();
                case KuduType.Int16:
                    return KuduEncoder.DecodeInt16(value).ToString();
                case KuduType.Int32:
                    return KuduEncoder.DecodeInt32(value).ToString();
                case KuduType.Int64:
                    return KuduEncoder.DecodeInt64(value).ToString();
                case KuduType.UnixtimeMicros:
                    return KuduEncoder.DecodeDateTime(value).ToString();
                case KuduType.Float:
                    return KuduEncoder.DecodeFloat(value).ToString();
                case KuduType.Double:
                    return KuduEncoder.DecodeDouble(value).ToString();
                case KuduType.String:
                    return $@"""{KuduEncoder.DecodeString(value)}""";
                case KuduType.Binary:
                    return BitConverter.ToString(value);
                case KuduType.Decimal32:
                    return DecodeDecimal(value).ToString();
                case KuduType.Decimal64:
                    return DecodeDecimal(value).ToString();
                case KuduType.Decimal128:
                    return DecodeDecimal(value).ToString();

                default:
                    throw new Exception($"Unknown column type {Column.Type}");
            }
        }

        private decimal DecodeDecimal(ReadOnlySpan<byte> value)
        {
            int scale = Column.TypeAttributes.Scale;

            return KuduEncoder.DecodeDecimal(value, Column.Type, scale);
        }

        /// <summary>
        /// Creates a new <see cref="KuduPredicate"/> on a boolean column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, bool value)
        {
            CheckColumn(column, KuduType.Bool);

            // Create the comparison predicate. Range predicates on boolean values can
            // always be converted to either an equality, an IS NOT NULL (filtering only
            // null values), or NONE (filtering all values).

            switch(op)
            {
                case ComparisonOp.Equal:
                    return EqualPredicate(column, value);

                // b > true  -> b NONE
                // b > false -> b = true
                case ComparisonOp.Greater:
                    return value ? None(column) : EqualPredicate(column, true);

                // b >= true  -> b = true
                // b >= false -> b IS NOT NULL
                case ComparisonOp.GreaterEqual:
                    return value ? EqualPredicate(column, true) : NewIsNotNullPredicate(column);

                // b < true  -> b = false
                // b < false -> b NONE
                case ComparisonOp.Less:
                    return value ? EqualPredicate(column, false) : None(column);

                // b <= true  -> b IS NOT NULL
                // b <= false -> b = false
                case ComparisonOp.LessEqual:
                    return value ? NewIsNotNullPredicate(column) : EqualPredicate(column, false);

                default:
                    throw new Exception($"Unknown ComparisonOp {op}");
            }
        }

        /// <summary>
        /// Creates a new comparison predicate on an integer or timestamp column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, long value)
        {
            //checkColumn(column, Type.INT8, Type.INT16, Type.INT32, Type.INT64, Type.UNIXTIME_MICROS);
            long minValue = MinIntValue(column.Type);
            long maxValue = MaxIntValue(column.Type);
            //Preconditions.checkArgument(value <= maxValue && value >= minValue,
            //                            "integer value out of range for %s column: %s",
            //                            column.getType(), value);

            return NewComparisonPredicate(column, op, value, minValue, maxValue);
        }

        /// <summary>
        /// Creates a new comparison predicate on an integer or timestamp column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        /// <param name="minValue">The minimum value for the column.</param>
        /// <param name="maxValue">The maximum value for the column.</param>
        private static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, long value, long minValue, long maxValue)
        {
            if (op == ComparisonOp.LessEqual)
            {
                if (value == maxValue)
                {
                    // If the value can't be incremented because it is at the top end of the
                    // range, then substitute the predicate with an IS NOT NULL predicate.
                    // This has the same effect as an inclusive upper bound on the maximum
                    // value. If the column is not nullable then the IS NOT NULL predicate
                    // is ignored.
                    return NewIsNotNullPredicate(column);
                }
                value += 1;
                op = ComparisonOp.Less;
            }
            else if (op == ComparisonOp.Greater)
            {
                if (value == maxValue)
                {
                    return None(column);
                }
                value += 1;
                op = ComparisonOp.GreaterEqual;
            }

            var bytes = GetBinary(column.Type, value);

            switch(op)
            {
                case ComparisonOp.GreaterEqual:
                    if (value == minValue) return NewIsNotNullPredicate(column);
                    else if (value == maxValue) return EqualPredicate(column, bytes);
                    else return new KuduPredicate(PredicateType.Range, column, bytes, null);

                case ComparisonOp.Equal:
                    return EqualPredicate(column, bytes);

                case ComparisonOp.Less:
                    if (value == minValue) return None(column);
                    else return new KuduPredicate(PredicateType.Range, column, null, bytes);

                default:
                    throw new Exception($"Unknown ComparisonOp {op}");
            }
        }

        /// <summary>
        /// Creates a new comparison predicate on a timestamp column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, DateTime value)
        {
            CheckColumn(column, KuduType.UnixtimeMicros);
            long micros = EpochTime.ToUnixEpochMicros(value);
            return NewComparisonPredicate(column, op, micros);
        }

        /// <summary>
        /// Creates a new comparison predicate on a float column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, float value)
        {
            CheckColumn(column, KuduType.Float);

            if (op == ComparisonOp.LessEqual)
            {
                if (value == float.PositiveInfinity)
                {
                    return NewIsNotNullPredicate(column);
                }
                value = value.NextUp();
                op = ComparisonOp.Less;
            }
            else if (op == ComparisonOp.Greater)
            {
                if (value == float.PositiveInfinity)
                {
                    return None(column);
                }
                value = value.NextUp();
                op = ComparisonOp.GreaterEqual;
            }

            byte[] bytes = KuduEncoder.EncodeFloat(value);
            switch (op)
            {
                case ComparisonOp.GreaterEqual:
                    if (value == float.NegativeInfinity)
                    {
                        return NewIsNotNullPredicate(column);
                    }
                    else if (value == float.PositiveInfinity)
                    {
                        return new KuduPredicate(PredicateType.Equality, column, bytes, null);
                    }
                    return new KuduPredicate(PredicateType.Range, column, bytes, null);
                case ComparisonOp.Equal:
                    return new KuduPredicate(PredicateType.Equality, column, bytes, null);
                case ComparisonOp.Less:
                    if (value == float.NegativeInfinity)
                    {
                        return None(column);
                    }
                    return new KuduPredicate(PredicateType.Range, column, null, bytes);
                default:
                    throw new Exception($"Unknown ComparisonOp {op}");
            }
        }

        /// <summary>
        /// Creates a new comparison predicate on a double column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, double value)
        {
            CheckColumn(column, KuduType.Double);
            if (op == ComparisonOp.LessEqual)
            {
                if (value == double.PositiveInfinity)
                {
                    return NewIsNotNullPredicate(column);
                }
                value = value.NextUp();
                op = ComparisonOp.Less;
            }
            else if (op == ComparisonOp.Greater)
            {
                if (value == double.PositiveInfinity)
                {
                    return None(column);
                }
                value = value.NextUp();
                op = ComparisonOp.GreaterEqual;
            }

            byte[] bytes = KuduEncoder.EncodeDouble(value);
            switch (op)
            {
                case ComparisonOp.GreaterEqual:
                    if (value == double.NegativeInfinity)
                    {
                        return NewIsNotNullPredicate(column);
                    }
                    else if (value == double.PositiveInfinity)
                    {
                        return new KuduPredicate(PredicateType.Equality, column, bytes, null);
                    }
                    return new KuduPredicate(PredicateType.Range, column, bytes, null);
                case ComparisonOp.Equal:
                    return new KuduPredicate(PredicateType.Equality, column, bytes, null);
                case ComparisonOp.Less:
                    if (value == double.NegativeInfinity)
                    {
                        return None(column);
                    }
                    return new KuduPredicate(PredicateType.Range, column, null, bytes);
                default:
                    throw new Exception($"Unknown ComparisonOp {op}");
            }
        }

        /// <summary>
        /// Creates a new comparison predicate on a decimal column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, decimal value)
        {
            //checkColumn(column, Type.DECIMAL);
            var typeAttributes = column.TypeAttributes;
            int precision = typeAttributes.Precision;
            int scale = typeAttributes.Scale;

            long maxValue;
            long longValue;

            switch (column.Type)
            {
                case KuduType.Decimal32:
                    maxValue = DecimalUtil.MaxDecimal32(precision);
                    longValue = DecimalUtil.EncodeDecimal32(value, precision, scale);
                    break;

                case KuduType.Decimal64:
                    maxValue = DecimalUtil.MaxDecimal64(precision);
                    longValue = DecimalUtil.EncodeDecimal64(value, precision, scale);
                    break;

                case KuduType.Decimal128:
                    return NewComparisonPredicate(column, op,
                        DecimalUtil.EncodeDecimal128(value, precision, scale),
                        DecimalUtil.MinDecimal128(precision),
                        DecimalUtil.MaxDecimal128(precision));

                default:
                    throw new Exception($"Unknown column type {column.Type}");
            }

            //Preconditions.checkArgument(value.compareTo(maxValue) <= 0 && value.compareTo(minValue) >= 0,
            //    "Decimal value out of range for %s column: %s",
            //    column.getType(), value);
            //BigDecimal smallestValue = DecimalUtil.smallestValue(scale);

            long minValue = maxValue * -1;

            return NewComparisonPredicate(column, op, longValue, minValue, maxValue);
        }

        /// <summary>
        /// Creates a new comparison predicate on an integer or timestamp column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        /// <param name="minValue">TODO</param>
        /// <param name="maxValue">TODO</param>
        private static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, KuduInt128 value, KuduInt128 minValue, KuduInt128 maxValue)
        {
            if (op == ComparisonOp.LessEqual)
            {
                if (value == maxValue)
                {
                    // If the value can't be incremented because it is at the top end of the
                    // range, then substitute the predicate with an IS NOT NULL predicate.
                    // This has the same effect as an inclusive upper bound on the maximum
                    // value. If the column is not nullable then the IS NOT NULL predicate
                    // is ignored.
                    return NewIsNotNullPredicate(column);
                }
                value += 1;
                op = ComparisonOp.Less;
            }
            else if (op == ComparisonOp.Greater)
            {
                if (value == maxValue)
                {
                    return None(column);
                }
                value += 1;
                op = ComparisonOp.GreaterEqual;
            }

            var bytes = KuduEncoder.EncodeInt128(value);

            switch(op)
            {
                case ComparisonOp.GreaterEqual:
                    if (value == minValue) return NewIsNotNullPredicate(column);
                    else if (value == maxValue) return EqualPredicate(column, bytes);
                    else return new KuduPredicate(PredicateType.Range, column, bytes, null);

                case ComparisonOp.Equal:
                    return EqualPredicate(column, bytes);

                case ComparisonOp.Less:
                    return (value == minValue)
                        ? None(column)
                        : new KuduPredicate(PredicateType.Range, column, null, bytes);

                default:
                    throw new Exception($"Unknown ComparisonOp {op}");
            }
        }

        /// <summary>
        /// Creates a new comparison predicate on a string column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, string value)
        {
            CheckColumn(column, KuduType.String);

            var bytes = KuduEncoder.EncodeString(value);
            return NewComparisonPredicateNoCheck(column, op, bytes);
        }

        /// <summary>
        /// Creates a new comparison predicate on a binary or string column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        public static KuduPredicate NewComparisonPredicate(
            ColumnSchema column, ComparisonOp op, byte[] value)
        {
            CheckColumn(column, KuduType.Binary);

            return NewComparisonPredicateNoCheck(column, op, value);
        }

        /// <summary>
        /// Creates a new comparison predicate on a binary or string column.
        /// </summary>
        /// <param name="column">The column schema.</param>
        /// <param name="op">The comparison operation.</param>
        /// <param name="value">The value to compare against.</param>
        private static KuduPredicate NewComparisonPredicateNoCheck(
            ColumnSchema column, ComparisonOp op, byte[] value)
        {
            if (op == ComparisonOp.LessEqual)
            {
                Array.Resize(ref value, value.Length + 1);
                op = ComparisonOp.Less;
            }
            else if (op == ComparisonOp.Greater)
            {
                Array.Resize(ref value, value.Length + 1);
                op = ComparisonOp.GreaterEqual;
            }

            switch(op)
            {
                case ComparisonOp.GreaterEqual:
                    if (value.Length == 0) return NewIsNotNullPredicate(column);
                    else return new KuduPredicate(PredicateType.Range, column, value, null);

                case ComparisonOp.Equal:
                    return EqualPredicate(column, value);

                case ComparisonOp.Less:
                    return (value.Length == 0)
                        ? None(column)
                        : new KuduPredicate(PredicateType.Range, column, null, value);

                default:
                    throw new Exception($"Unknown ComparisonOp {op}");
            };
        }

        public static KuduPredicate None(ColumnSchema column)
        {
            return new KuduPredicate(PredicateType.None, column, null, null);
        }

        private static KuduPredicate EqualPredicate(ColumnSchema column, bool value)
        {
            var binary = KuduEncoder.EncodeBool(value);
            return EqualPredicate(column, binary);
        }

        private static KuduPredicate EqualPredicate(ColumnSchema column, byte[] value)
        {
            return new KuduPredicate(PredicateType.Equality, column, value, null);
        }

        /// <summary>
        /// Creates a new IsNotNull predicate.
        /// </summary>
        /// <param name="column">The column that the predicate applies to.</param>
        public static KuduPredicate NewIsNotNullPredicate(ColumnSchema column)
        {
            return new KuduPredicate(PredicateType.IsNotNull, column, null, null);
        }

        /// <summary>
        /// Creates a new IsNull predicate.
        /// </summary>
        /// <param name="column">The column that the predicate applies to.</param>
        public static KuduPredicate NewIsNullPredicate(ColumnSchema column)
        {
            if (!column.IsNullable)
                return None(column);

            return new KuduPredicate(PredicateType.IsNull, column, null, null);
        }

        private static SortedSet<byte[]> GetZ<K>(ColumnSchema column, IEnumerable<K> setx, Func<K, byte[]> conv)
        {
            // TODO: Avoid closure
            var comparer = new PredicateComparer(column);
            var values = new SortedSet<byte[]>(comparer);
            var result = new SortedSet<byte[]>(comparer);
            foreach (var i in setx)
            {
                var bytes = conv(i);
                result.Add(bytes);
            }

            return result;
        }

        public static KuduPredicate NewInListPredicate<T>(
            ColumnSchema column, IEnumerable<T> values) {

            SortedSet<byte[]> z;

            switch (values) {
                case IEnumerable<bool> x:
                    z = GetZ(column, x, KuduEncoder.EncodeBool);
                    break;
                case IEnumerable<sbyte> x:
                    z = GetZ(column, x, KuduEncoder.EncodeInt8);
                    break;
                case IEnumerable<short> x:
                    z = GetZ(column, x, KuduEncoder.EncodeInt16);
                    break;
                case IEnumerable<int> x:
                    z = GetZ(column, x, KuduEncoder.EncodeInt32);
                    break;
                case IEnumerable<long> x:
                    z = GetZ(column, x, KuduEncoder.EncodeInt64);
                    break;
                case IEnumerable<float> x:
                    z = GetZ(column, x, KuduEncoder.EncodeFloat);
                    break;
                case IEnumerable<double> x:
                    z = GetZ(column, x, KuduEncoder.EncodeDouble);
                    break;
                case IEnumerable<string> x:
                    z = GetZ(column, x, KuduEncoder.EncodeString);
                    break;
                case IEnumerable<byte[]> x:
                    z = GetZ(column, x, y => y);
                    break;
                case IEnumerable<decimal> x:
                    if (column.Type == KuduType.Decimal32)
                        z = GetZ(column, x, i => KuduEncoder.EncodeDecimal32(
                            i, column.TypeAttributes.Precision, column.TypeAttributes.Scale));
                    else if (column.Type == KuduType.Decimal64)
                        z = GetZ(column, x, i => KuduEncoder.EncodeDecimal64(
                            i, column.TypeAttributes.Precision, column.TypeAttributes.Scale));
                    else if (column.Type == KuduType.Decimal128)
                        z = GetZ(column, x, i => KuduEncoder.EncodeDecimal128(
                            i, column.TypeAttributes.Precision, column.TypeAttributes.Scale));
                    else throw new Exception();
                    break;
                default:
                    throw new Exception();
            }

            return BuildInList(column, z);
        }

        private static long MinIntValue(KuduType type)
        {
            switch(type)
            {
                case KuduType.Int8:
                    return sbyte.MinValue;
                case KuduType.Int16:
                    return short.MinValue;
                case KuduType.Int32:
                    return int.MinValue;
                case KuduType.Int64:
                    return long.MinValue;
                case KuduType.UnixtimeMicros:
                    return long.MinValue;
                default:
                    throw new Exception();
            };
        }

        private static long MaxIntValue(KuduType type)
        {
            switch(type)
            {
                case KuduType.Int8:
                    return sbyte.MaxValue;
                case KuduType.Int16:
                    return short.MaxValue;
                case KuduType.Int32:
                    return int.MaxValue;
                case KuduType.Int64:
                    return long.MaxValue;
                case KuduType.UnixtimeMicros:
                    return long.MaxValue;
                default:
                    throw new Exception();
            }
        }

        private static byte[] GetBinary(KuduType type, long value)
        {
            switch (type) {
                case KuduType.Int8:
                    return KuduEncoder.EncodeInt8((sbyte) value);
                case KuduType.Int16:
                    return KuduEncoder.EncodeInt16((short) value);
                case KuduType.Int32:
                    return KuduEncoder.EncodeInt32((int) value);
                case KuduType.Decimal32:
                    return KuduEncoder.EncodeInt32((int) value);
                case KuduType.Int64:
                    return KuduEncoder.EncodeInt64(value);
                case KuduType.UnixtimeMicros:
                    return KuduEncoder.EncodeInt64(value);
                case KuduType.Decimal64:
                    return KuduEncoder.EncodeInt64(value);
                default:
                    throw new Exception();
            }
        }

        /// <summary>
        /// Compares two bounds based on the type of the column.
        /// </summary>
        /// <param name="column">The column which the values belong to.</param>
        /// <param name="a">The first serialized value.</param>
        /// <param name="b">The second serialized value.</param>
        private static int Compare(ColumnSchema column, byte[] a, byte[] b)
        {
            switch (column.Type)
            {
                case KuduType.Bool:
                    return KuduEncoder.DecodeBool(a).CompareTo(KuduEncoder.DecodeBool(b));
                case KuduType.Int8:
                    return KuduEncoder.DecodeInt8(a).CompareTo(KuduEncoder.DecodeInt8(b));
                case KuduType.Int16:
                    return KuduEncoder.DecodeInt16(a).CompareTo(KuduEncoder.DecodeInt16(b));
                case KuduType.Int32:
                case KuduType.Decimal32:
                    return KuduEncoder.DecodeInt32(a).CompareTo(KuduEncoder.DecodeInt32(b));
                case KuduType.Int64:
                case KuduType.UnixtimeMicros:
                case KuduType.Decimal64:
                    return KuduEncoder.DecodeInt64(a).CompareTo(KuduEncoder.DecodeInt64(b));
                case KuduType.Float:
                    return KuduEncoder.DecodeFloat(a).CompareTo(KuduEncoder.DecodeFloat(b));
                case KuduType.Double:
                    return KuduEncoder.DecodeDouble(a).CompareTo(KuduEncoder.DecodeDouble(b));
                case KuduType.String:
                case KuduType.Binary:
                    return a.AsSpan().SequenceCompareTo(b);
                case KuduType.Decimal128:
                    return KuduEncoder.DecodeInt128(a).CompareTo(KuduEncoder.DecodeInt128(b));
                default:
                    throw new Exception($"Unknown column type {column.Type}");
            }
        }

        /// <summary>
        /// Returns true if increment(a) == b.
        /// </summary>
        /// <param name="a">The value which would be incremented.</param>
        /// <param name="b">The target value.</param>
        private bool AreConsecutive(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
        {
            switch (Column.Type)
            {
                case KuduType.Bool: return false;
                case KuduType.Int8:
                    {
                        sbyte m = KuduEncoder.DecodeInt8(a);
                        sbyte n = KuduEncoder.DecodeInt8(b);
                        return m < n && m + 1 == n;
                    }
                case KuduType.Int16:
                    {
                        short m = KuduEncoder.DecodeInt16(a);
                        short n = KuduEncoder.DecodeInt16(b);
                        return m < n && m + 1 == n;
                    }
                case KuduType.Int32:
                case KuduType.Decimal32:
                    {
                        int m = KuduEncoder.DecodeInt32(a);
                        int n = KuduEncoder.DecodeInt32(b);
                        return m < n && m + 1 == n;
                    }
                case KuduType.Int64:
                case KuduType.UnixtimeMicros:
                case KuduType.Decimal64:
                    {
                        long m = KuduEncoder.DecodeInt64(a);
                        long n = KuduEncoder.DecodeInt64(b);
                        return m < n && m + 1 == n;
                    }
                case KuduType.Float:
                    {
                        float m = KuduEncoder.DecodeFloat(a);
                        float n = KuduEncoder.DecodeFloat(b);
                        return m < n && m.NextUp() == n;
                    }
                case KuduType.Double:
                    {
                        double m = KuduEncoder.DecodeDouble(a);
                        double n = KuduEncoder.DecodeDouble(b);
                        return m < n && m.NextUp() == n;
                    }
                case KuduType.String:
                case KuduType.Binary:
                    {
                        if (a.Length + 1 != b.Length || b[a.Length] != 0)
                        {
                            return false;
                        }

                        return a.SequenceEqual(b.Slice(0, a.Length));
                    }
                case KuduType.Decimal128:
                    {
                        KuduInt128 m = KuduEncoder.DecodeInt128(a);
                        KuduInt128 n = KuduEncoder.DecodeInt128(b);

                        return m < n && (m + 1) == n;
                    }
                default:
                    throw new Exception($"Unknown column type {Column.Type}");
            }
        }

        /// <summary>
        /// Builds an IN list predicate from a collection of raw values. The collection
        /// must be sorted and deduplicated.
        /// </summary>
        /// <param name="column">The column.</param>
        /// <param name="values">The IN list values.</param>
        private static KuduPredicate BuildInList(ColumnSchema column, SortedSet<byte[]> values)
        {
            var numPredicates = values.Count;

            // IN (true, false) predicates can be simplified to IS NOT NULL.
            if (column.Type == KuduType.Bool && numPredicates > 1)
            {
                return NewIsNotNullPredicate(column);
            }

            switch (numPredicates) {
                case 0:
                    return None(column);
                case 1:
                    return EqualPredicate(column, values.Min);
                default:
                    return new KuduPredicate(column, values);
            }
        }

        /// <summary>
        /// Check if this RANGE predicate contains the value.
        /// </summary>
        /// <param name="value">The value to check.</param>
        private bool RangeContains(byte[] value)
        {
            return (Lower == null || Compare(Column, value, Lower) >= 0) &&
                   (Upper == null || Compare(Column, value, Upper) < 0);
        }

        /// <summary>
        /// Checks that the column is one of the expected types.
        /// </summary>
        /// <param name="column">The column being checked.</param>
        /// <param name="type">The expected type.</param>
        private static void CheckColumn(ColumnSchema column, KuduType type)
        {
            if (column.Type != type)
            {
                throw new ArgumentException($"Expected type {type} but received {column.Type}");
            }
        }

        private sealed class PredicateComparer : IComparer<byte[]>
        {
            private readonly ColumnSchema _column;

            public PredicateComparer(ColumnSchema column)
            {
                _column = column;
            }

            public int Compare(byte[] x, byte[] y)
            {
                return KuduPredicate.Compare(_column, x, y);
            }
        }
    }

    public enum PredicateType
    {
        /// <summary>
        /// A predicate which filters all rows.
        /// </summary>
        None,
        /// <summary>
        /// A predicate which filters all rows not equal to a value.
        /// </summary>
        Equality,
        /// <summary>
        /// A predicate which filters all rows not in a range.
        /// </summary>
        Range,
        /// <summary>
        /// A predicate which filters all null rows.
        /// </summary>
        IsNotNull,
        /// <summary>
        /// A predicate which filters all non-null rows.
        /// </summary>
        IsNull,
        /// <summary>
        /// A predicate which filters all rows not matching a list of values.
        /// </summary>
        InList
    }

    /// <summary>
    /// The comparison operator of a predicate.
    /// </summary>
    public enum ComparisonOp
    {
        Greater,
        GreaterEqual,
        Equal,
        Less,
        LessEqual
    }
}
