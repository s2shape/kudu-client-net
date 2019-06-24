﻿using System.Buffers.Binary;
using Kudu.Client.Builder;
using Kudu.Client.Util;
using Xunit;

namespace Kudu.Client.Tests
{
    public class KuduPredicateTests
    {
        private readonly ColumnSchema boolCol = CreateColumnSchema("bool", DataType.Bool);
        private readonly ColumnSchema byteCol = CreateColumnSchema("byte", DataType.Int8);
        private readonly ColumnSchema shortCol = CreateColumnSchema("short", DataType.Int16);
        private readonly ColumnSchema intCol = CreateColumnSchema("int", DataType.Int32);
        private readonly ColumnSchema longCol = CreateColumnSchema("long", DataType.Int64);
        private readonly ColumnSchema floatCol = CreateColumnSchema("float", DataType.Float);
        private readonly ColumnSchema doubleCol = CreateColumnSchema("double", DataType.Double);
        private readonly ColumnSchema stringCol = CreateColumnSchema("string", DataType.String);
        private readonly ColumnSchema binaryCol = CreateColumnSchema("binary", DataType.Binary);

        /// <summary>
        /// Tests merges on all types of integer predicates.
        /// </summary>
        [Fact]
        public void TestMergeInt()
        {
            // Equality + Equality
            //--------------------

            // |
            // |
            // =
            // |
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0));
            // |
            //  |
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 1),
                      KuduPredicate.None(intCol));

            // Range + Equality
            //--------------------

            // [-------->
            //      |
            // =
            //      |
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 10),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 10));

            //    [-------->
            //  |
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 10),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0),
                      KuduPredicate.None(intCol));

            // <--------)
            //      |
            // =
            //      |
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 10),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5));

            // <--------)
            //            |
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 10),
                      KuduPredicate.None(intCol));

            // Unbounded Range + Unbounded Range
            //--------------------

            // [--------> AND
            // [-------->
            // =
            // [-------->

            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0));

            // [--------> AND
            //    [----->
            // =
            //    [----->
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5));

            // <--------) AND
            // <--------)
            // =
            // <--------)

            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 0));

            // <--------) AND
            // <----)
            // =
            // <----)

            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, -10),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, -10));

            //    [--------> AND
            // <-------)
            // =
            //    [----)
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 10),
                      IntRange(0, 10));

            //     [-----> AND
            // <----)
            // =
            //     |
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 6),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5));

            //     [-----> AND
            // <---)
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 5),
                      KuduPredicate.None(intCol));

            //       [-----> AND
            // <---)
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 3),
                      KuduPredicate.None(intCol));

            // Range + Range
            //--------------------

            // [--------) AND
            // [--------)
            // =
            // [--------)

            TestMerge(IntRange(0, 10),
                      IntRange(0, 10),
                      IntRange(0, 10));

            // [--------) AND
            // [----)
            // =
            // [----)
            TestMerge(IntRange(0, 10),
                      IntRange(0, 5),
                      IntRange(0, 5));

            // [--------) AND
            //   [----)
            // =
            //   [----)
            TestMerge(IntRange(0, 10),
                      IntRange(3, 8),
                      IntRange(3, 8));

            // [-----) AND
            //   [------)
            // =
            //   [---)
            TestMerge(IntRange(0, 8),
                      IntRange(3, 10),
                      IntRange(3, 8));
            // [--) AND
            //    [---)
            // =
            // None
            TestMerge(IntRange(0, 5),
                      IntRange(5, 10),
                      KuduPredicate.None(intCol));

            // [--) AND
            //       [---)
            // =
            // None
            TestMerge(IntRange(0, 3),
                      IntRange(5, 10),
                      KuduPredicate.None(intCol));

            // Lower Bound + Range
            //--------------------

            // [------------>
            //       [---)
            // =
            //       [---)
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      IntRange(5, 10),
                      IntRange(5, 10));

            // [------------>
            // [--------)
            // =
            // [--------)
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5),
                      IntRange(5, 10),
                      IntRange(5, 10));

            //      [------------>
            // [--------)
            // =
            //      [---)
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5),
                      IntRange(0, 10),
                      IntRange(5, 10));

            //          [------->
            // [-----)
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 10),
                      IntRange(0, 5),
                      KuduPredicate.None(intCol));

            // Upper Bound + Range
            //--------------------

            // <------------)
            //       [---)
            // =
            //       [---)
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 10),
                      IntRange(3, 8),
                      IntRange(3, 8));

            // <------------)
            //     [--------)
            // =
            //     [--------)
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 10),
                      IntRange(5, 10),
                      IntRange(5, 10));


            // <------------)
            //         [--------)
            // =
            //         [----)
            TestMerge(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 5),
                      IntRange(0, 10),
                      IntRange(0, 5));

            // Range + Equality
            //--------------------

            //   [---) AND
            // |
            // =
            // None
            TestMerge(IntRange(3, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 1),
                      KuduPredicate.None(intCol));

            // [---) AND
            // |
            // =
            // |
            TestMerge(IntRange(0, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0));

            // [---) AND
            //   |
            // =
            //   |
            TestMerge(IntRange(0, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 3),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 3));

            // [---) AND
            //     |
            // =
            // None
            TestMerge(IntRange(0, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5),
                      KuduPredicate.None(intCol));

            // [---) AND
            //       |
            // =
            // None
            TestMerge(IntRange(0, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 7),
                      KuduPredicate.None(intCol));

            // IN list + IN list
            //--------------------

            // | | |
            //   | | |
            TestMerge(IntInList(0, 10, 20),
                      IntInList(20, 10, 20, 30),
                      IntInList(10, 20));

            // |   |
            //    | |
            TestMerge(IntInList(0, 20),
                      IntInList(15, 30),
                      KuduPredicate.None(intCol));

            // IN list + NOT NULL
            //--------------------

            TestMerge(IntInList(10),
                      KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 10));

            TestMerge(IntInList(10, -100),
                      KuduPredicate.NewIsNotNullPredicate(intCol),
                      IntInList(-100, 10));

            // IN list + Equality
            //--------------------

            // | | |
            //   |
            // =
            //   |
            TestMerge(IntInList(0, 10, 20),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 10),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 10));

            // | | |
            //       |
            // =
            // none
            TestMerge(IntInList(0, 10, 20),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 30),
                      KuduPredicate.None(intCol));

            // IN list + Range
            //--------------------

            // | | | | |
            //   [---)
            // =
            //   | |
            TestMerge(IntInList(0, 10, 20, 30, 40),
                      IntRange(10, 30),
                      IntInList(10, 20));

            // | |   | |
            //    [--)
            // =
            // none
            TestMerge(IntInList(0, 10, 20, 30),
                      IntRange(25, 30),
                      KuduPredicate.None(intCol));

            // | | | |
            //    [------>
            // =
            //   | |
            TestMerge(IntInList(0, 10, 20, 30),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 15),
                      IntInList(20, 30));

            // | | |
            //    [------>
            // =
            //     |
            TestMerge(IntInList(0, 10, 20),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 15),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 20));

            // | |
            //    [------>
            // =
            // none
            TestMerge(IntInList(0, 10),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 15),
                      KuduPredicate.None(intCol));

            // | | | |
            // <--)
            // =
            // | |
            TestMerge(IntInList(0, 10, 20, 30),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 15),
                      IntInList(0, 10));

            // |  | |
            // <--)
            // =
            // |
            TestMerge(IntInList(0, 10, 20),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 10),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 0));

            //      | |
            // <--)
            // =
            // none
            TestMerge(IntInList(10, 20),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 5),
                      KuduPredicate.None(intCol));

            // None
            //--------------------

            // None AND
            // [---->
            // =
            // None
            TestMerge(KuduPredicate.None(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.None(intCol));

            // None AND
            // <----)
            // =
            // None
            TestMerge(KuduPredicate.None(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 0),
                      KuduPredicate.None(intCol));

            // None AND
            // [----)
            // =
            // None
            TestMerge(KuduPredicate.None(intCol),
                      IntRange(3, 7),
                      KuduPredicate.None(intCol));

            // None AND
            //  |
            // =
            // None
            TestMerge(KuduPredicate.None(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5),
                      KuduPredicate.None(intCol));

            // None AND
            // None
            // =
            // None
            TestMerge(KuduPredicate.None(intCol),
                      KuduPredicate.None(intCol),
                      KuduPredicate.None(intCol));

            // IS NOT NULL
            //--------------------

            // IS NOT NULL AND
            // NONE
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.None(intCol),
                      KuduPredicate.None(intCol));

            // IS NOT NULL AND
            // IS NULL
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.None(intCol));

            // IS NOT NULL AND
            // IS NOT NULL
            // =
            // IS NOT NULL
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.NewIsNotNullPredicate(intCol));

            // IS NOT NULL AND
            // |
            // =
            // |
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5));

            // IS NOT NULL AND
            // [------->
            // =
            // [------->
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 5));

            // IS NOT NULL AND
            // <---------)
            // =
            // <---------)
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 5),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 5));

            // IS NOT NULL AND
            // [-------)
            // =
            // [-------)
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      IntRange(0, 12),
                      IntRange(0, 12));


            // IS NOT NULL AND
            // |   |   |
            // =
            // |   |   |
            TestMerge(KuduPredicate.NewIsNotNullPredicate(intCol),
                      IntInList(0, 10, 20),
                      IntInList(0, 10, 20));

            // IS NULL
            //--------------------

            // IS NULL AND
            // NONE
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.None(intCol),
                      KuduPredicate.None(intCol));

            // IS NULL AND
            // IS NULL
            // =
            // IS_NULL
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.NewIsNullPredicate(intCol));

            // IS NULL AND
            // IS NOT NULL
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.NewIsNotNullPredicate(intCol),
                      KuduPredicate.None(intCol));

            // IS NULL AND
            // |
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, 5),
                      KuduPredicate.None(intCol));

            // IS NULL AND
            // [------->
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.None(intCol));

            // IS NULL AND
            // <---------)
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 5),
                      KuduPredicate.None(intCol));

            // IS NULL AND
            // [-------)
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      IntRange(0, 12),
                      KuduPredicate.None(intCol));

            // IS NULL AND
            // |   |   |
            // =
            // NONE
            TestMerge(KuduPredicate.NewIsNullPredicate(intCol),
                      IntInList(0, 10, 20),
                      KuduPredicate.None(intCol));
        }

        /// <summary>
        /// Tests tricky merges on a var length type.
        /// </summary>
        [Fact]
        public void TestMergeString()
        {
            //         [----->
            //  <-----)
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.GreaterEqual, "b\0"),
                      KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Less, "b"),
                      KuduPredicate.None(stringCol));

            //        [----->
            //  <-----)
            // =
            // None
            TestMerge(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.GreaterEqual, "b"),
                      KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Less, "b"),
                      KuduPredicate.None(stringCol));

            //       [----->
            //  <----)
            // =
            //       |
            TestMerge(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.GreaterEqual, "b"),
                      KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Less, "b\0"),
                      KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Equal, "b"));

            //     [----->
            //  <-----)
            // =
            //     [--)
            TestMerge(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.GreaterEqual, "a"),
                      KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Less, "a\0\0"),
                      new KuduPredicate(PredicateType.Range, stringCol,
                                        KuduEncoder.EncodeString("a"), KuduEncoder.EncodeString("a\0\0")));

            //     [----->
            //   | | | |
            // =
            //     | | |
            TestMerge(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.GreaterEqual, "a"),
                      StringInList("a", "c", "b", ""),
                      StringInList("a", "b", "c"));

            //   IS NOT NULL
            //   | | | |
            // =
            //   | | | |
            TestMerge(KuduPredicate.NewIsNotNullPredicate(stringCol),
                      StringInList("a", "c", "b", ""),
                      StringInList("", "a", "b", "c"));
        }

        [Fact]
        public void TestBoolean()
        {
            // b >= false
            Assert.Equal(KuduPredicate.NewIsNotNullPredicate(boolCol),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.GreaterEqual, false));
            // b > false
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, true),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Greater, false));
            // b = false
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, false),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, false));
            // b < false
            Assert.Equal(KuduPredicate.None(boolCol),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Less, false));
            // b <= false
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, false),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.LessEqual, false));

            // b >= true
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, true),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.GreaterEqual, true));
            // b > true
            Assert.Equal(KuduPredicate.None(boolCol),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Greater, true));
            // b = true
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, true),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, true));
            // b < true
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, false),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Less, true));
            // b <= true
            Assert.Equal(KuduPredicate.NewIsNotNullPredicate(boolCol),
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.LessEqual, true));

            // b IN ()
            Assert.Equal(KuduPredicate.None(boolCol), BoolInList());

            // b IN (true)
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, true),
                         BoolInList(true, true, true));

            // b IN (false)
            Assert.Equal(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, false),
                         BoolInList(false));

            // b IN (false, true)
            Assert.Equal(KuduPredicate.NewIsNotNullPredicate(boolCol),
                         BoolInList(false, true, false, true));
        }

        /// <summary>
        /// Tests basic predicate merges across all types.
        /// </summary>
        [Fact]
        public void TestAllTypesMerge()
        {
            TestMerge(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.GreaterEqual, false),
              KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Less, true),
              new KuduPredicate(PredicateType.Equality,
                                boolCol,
                                KuduEncoder.EncodeBool(false),
                                null));

            TestMerge(KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.GreaterEqual, false),
                      KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.LessEqual, true),
                      KuduPredicate.NewIsNotNullPredicate(boolCol));

            TestMerge(KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.Less, 10),
                      new KuduPredicate(PredicateType.Range,
                                        byteCol,
                                        new byte[] { 0 },
                                        new byte[] { 10 }));

            TestMerge(KuduPredicate.NewInListPredicate(byteCol, new byte[] { 12, 14, 16, 18 }),
                      KuduPredicate.NewInListPredicate(byteCol, new byte[] { 14, 18, 20 }),
                      KuduPredicate.NewInListPredicate(byteCol, new byte[] { 14, 18 }));

            TestMerge(KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.Less, 10),
                      new KuduPredicate(PredicateType.Range,
                                        shortCol,
                                        KuduEncoder.EncodeInt16(0),
                                        KuduEncoder.EncodeInt16(10)));

            TestMerge(KuduPredicate.NewInListPredicate(shortCol, new short[] { 12, 14, 16, 18 }),
                      KuduPredicate.NewInListPredicate(shortCol, new short[] { 14, 18, 20 }),
                      KuduPredicate.NewInListPredicate(shortCol, new short[] { 14, 18 }));

            TestMerge(KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.GreaterEqual, 0),
                      KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.Less, 10),
                      new KuduPredicate(PredicateType.Range,
                                        longCol,
                                        KuduEncoder.EncodeInt64(0),
                                        KuduEncoder.EncodeInt64(10)));

            TestMerge(KuduPredicate.NewInListPredicate(longCol, new long[] { 12, 14, 16, 18 }),
                      KuduPredicate.NewInListPredicate(longCol, new long[] { 14, 18, 20 }),
                      KuduPredicate.NewInListPredicate(longCol, new long[] { 14, 18 }));

            TestMerge(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.GreaterEqual, 123.45f),
                      KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Less, 678.90f),
                      new KuduPredicate(PredicateType.Range,
                                        floatCol,
                                        KuduEncoder.EncodeFloat(123.45f),
                                        KuduEncoder.EncodeFloat(678.90f)));

            TestMerge(KuduPredicate.NewInListPredicate(floatCol, new float[] { 12f, 14f, 16f, 18f }),
                      KuduPredicate.NewInListPredicate(floatCol, new float[] { 14f, 18f, 20f }),
                      KuduPredicate.NewInListPredicate(floatCol, new float[] { 14f, 18f }));

            TestMerge(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.GreaterEqual, 123.45),
                      KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Less, 678.90),
                      new KuduPredicate(PredicateType.Range,
                                        doubleCol,
                                        KuduEncoder.EncodeDouble(123.45),
                                        KuduEncoder.EncodeDouble(678.90)));

            TestMerge(KuduPredicate.NewInListPredicate(doubleCol, new double[] { 12d, 14d, 16d, 18d }),
                      KuduPredicate.NewInListPredicate(doubleCol, new double[] { 14d, 18d, 20d }),
                      KuduPredicate.NewInListPredicate(doubleCol, new double[] { 14d, 18d }));

            //TestMerge(KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.GreaterEqual,
            //    BigDecimal.valueOf(12345, 2)),
            //    KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.Less,
            //        BigDecimal.valueOf(67890, 2)),
            //    new KuduPredicate(PredicateType.Range,
            //        decimal32Col,
            //        Bytes.fromBigDecimal(BigDecimal.valueOf(12345, 2),
            //            DecimalUtil.MAX_DECIMAL32_PRECISION),
            //        Bytes.fromBigDecimal(BigDecimal.valueOf(67890, 2),
            //            DecimalUtil.MAX_DECIMAL32_PRECISION)));

            //TestMerge(KuduPredicate.NewInListPredicate(decimal32Col, ImmutableList.of(
            //        BigDecimal.valueOf(12345, 2),
            //        BigDecimal.valueOf(45678, 2))),
            //    KuduPredicate.NewInListPredicate(decimal32Col, ImmutableList.of(
            //        BigDecimal.valueOf(45678, 2),
            //        BigDecimal.valueOf(98765, 2))),
            //    KuduPredicate.NewInListPredicate(decimal32Col, ImmutableList.of(
            //        BigDecimal.valueOf(45678, 2))));

            //TestMerge(KuduPredicate.NewInListPredicate(decimal64Col, ImmutableList.of(
            //    BigDecimal.valueOf(12345678910L, 2),
            //    BigDecimal.valueOf(34567891011L, 2))),
            //    KuduPredicate.NewInListPredicate(decimal64Col, ImmutableList.of(
            //        BigDecimal.valueOf(34567891011L, 2),
            //        BigDecimal.valueOf(98765432111L, 2))),
            //    KuduPredicate.NewInListPredicate(decimal64Col, ImmutableList.of(
            //        BigDecimal.valueOf(34567891011L, 2))));

            //TestMerge(KuduPredicate.NewComparisonPredicate(decimal64Col, ComparisonOp.GreaterEqual,
            //    BigDecimal.valueOf(12345678910L, 2)),
            //    KuduPredicate.NewComparisonPredicate(decimal64Col, ComparisonOp.Less,
            //        BigDecimal.valueOf(67890101112L, 2)),
            //    new KuduPredicate(PredicateType.Range,
            //        decimal64Col,
            //        Bytes.fromBigDecimal(BigDecimal.valueOf(12345678910L, 2),
            //            DecimalUtil.MAX_DECIMAL64_PRECISION),
            //        Bytes.fromBigDecimal(BigDecimal.valueOf(67890101112L, 2),
            //            DecimalUtil.MAX_DECIMAL64_PRECISION)));

            //TestMerge(KuduPredicate.NewInListPredicate(decimal128Col, ImmutableList.of(
            //    new BigDecimal("1234567891011121314.15"),
            //    new BigDecimal("3456789101112131415.16"))),
            //    KuduPredicate.NewInListPredicate(decimal128Col, ImmutableList.of(
            //        new BigDecimal("3456789101112131415.16"),
            //        new BigDecimal("9876543212345678910.11"))),
            //    KuduPredicate.NewInListPredicate(decimal128Col, ImmutableList.of(
            //        new BigDecimal("3456789101112131415.16"))));

            //TestMerge(KuduPredicate.NewComparisonPredicate(decimal128Col, ComparisonOp.GreaterEqual,
            //    new BigDecimal("1234567891011121314.15")),
            //    KuduPredicate.NewComparisonPredicate(decimal128Col, ComparisonOp.Less,
            //        new BigDecimal("67891011121314151617.18")),
            //    new KuduPredicate(PredicateType.Range,
            //        decimal128Col,
            //        Bytes.fromBigDecimal(new BigDecimal("1234567891011121314.15"),
            //            DecimalUtil.MAX_DECIMAL128_PRECISION),
            //        Bytes.fromBigDecimal(new BigDecimal("67891011121314151617.18"),
            //            DecimalUtil.MAX_DECIMAL128_PRECISION)));

            TestMerge(KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.GreaterEqual,
                                                           new byte[] { 0, 1, 2, 3, 4, 5, 6 }),
                      KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.Less, new byte[] { 10 }),
                      new KuduPredicate(PredicateType.Range,
                                        binaryCol,
                                        new byte[] { 0, 1, 2, 3, 4, 5, 6 },
                                        new byte[] { 10 }));

            byte[] bA = "a".ToUtf8ByteArray();
            byte[] bB = "b".ToUtf8ByteArray();
            byte[] bC = "c".ToUtf8ByteArray();
            byte[] bD = "d".ToUtf8ByteArray();
            byte[] bE = "e".ToUtf8ByteArray();
            TestMerge(KuduPredicate.NewInListPredicate(binaryCol, new byte[][] { bA, bB, bC, bD }),
                      KuduPredicate.NewInListPredicate(binaryCol, new byte[][] { bB, bD, bE }),
                      KuduPredicate.NewInListPredicate(binaryCol, new byte[][] { bB, bD }));
        }

        [Fact]
        public void TestLessEqual()
        {
            Assert.Equal(KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.LessEqual, 10),
                         KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.Less, 11));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.LessEqual, 10),
                         KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.Less, 11));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.LessEqual, 10),
                         KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, 11));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.LessEqual, 10),
                         KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.Less, 11));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.LessEqual, 12.345f),
                         KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Less, 12.345f.NextUp()));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.LessEqual, 12.345),
                         KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Less, 12.345.NextUp()));
            //Assert.Equal(
            //    KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.LessEqual, BigDecimal.valueOf(12345, 2)),
            //    KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.Less, BigDecimal.valueOf(12346, 2)));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.LessEqual, "a"),
                         KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Less, "a\0"));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.LessEqual, new byte[] { 10 }),
                         KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.Less, new byte[] { 10, 0 }));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.LessEqual, sbyte.MaxValue),
                         KuduPredicate.NewIsNotNullPredicate(byteCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.LessEqual, short.MaxValue),
                         KuduPredicate.NewIsNotNullPredicate(shortCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.LessEqual, int.MaxValue),
                         KuduPredicate.NewIsNotNullPredicate(intCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.LessEqual, long.MaxValue),
                         KuduPredicate.NewIsNotNullPredicate(longCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.LessEqual, float.MaxValue),
                         KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Less, float.PositiveInfinity));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.LessEqual, float.PositiveInfinity),
                         KuduPredicate.NewIsNotNullPredicate(floatCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.LessEqual, double.MaxValue),
                         KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Less, double.PositiveInfinity));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.LessEqual, double.PositiveInfinity),
                         KuduPredicate.NewIsNotNullPredicate(doubleCol));
        }

        [Fact]
        public void TestGreater()
        {
            Assert.Equal(KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.GreaterEqual, 11),
                         KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.Greater, 10));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.GreaterEqual, 11),
                         KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.Greater, 10));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, 11),
                         KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Greater, 10));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.GreaterEqual, 11),
                         KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.Greater, 10));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.GreaterEqual, 12.345f.NextUp()),
                         KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Greater, 12.345f));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.GreaterEqual, 12.345.NextUp()),
                         KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Greater, 12.345));
            //Assert.Equal(
            //    KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.GreaterEqual, BigDecimal.valueOf(12346, 2)),
            //    KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.Greater, BigDecimal.valueOf(12345, 2)));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.GreaterEqual, "a\0"),
                         KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Greater, "a"));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.GreaterEqual, new byte[] { 10, 0 }),
                         KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.Greater, new byte[] { 10 }));

            Assert.Equal(KuduPredicate.None(byteCol),
                         KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.Greater, sbyte.MaxValue));
            Assert.Equal(KuduPredicate.None(shortCol),
                         KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.Greater, short.MaxValue));
            Assert.Equal(KuduPredicate.None(intCol),
                         KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Greater, int.MaxValue));
            Assert.Equal(KuduPredicate.None(longCol),
                         KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.Greater, long.MaxValue));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.GreaterEqual, float.PositiveInfinity),
                         KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Greater, float.MaxValue));
            Assert.Equal(KuduPredicate.None(floatCol),
                         KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Greater, float.PositiveInfinity));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.GreaterEqual, double.PositiveInfinity),
                         KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Greater, double.MaxValue));
            Assert.Equal(KuduPredicate.None(doubleCol),
                         KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Greater, double.PositiveInfinity));
        }

        [Fact]
        public void TestLess()
        {
            Assert.Equal(KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.Less, sbyte.MinValue),
                         KuduPredicate.None(byteCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.Less, short.MinValue),
                         KuduPredicate.None(shortCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Less, int.MinValue),
                         KuduPredicate.None(intCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.Less, long.MinValue),
                         KuduPredicate.None(longCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Less, float.NegativeInfinity),
                         KuduPredicate.None(floatCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Less, double.NegativeInfinity),
                         KuduPredicate.None(doubleCol));
            //Assert.Equal(KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.Less,
            //    DecimalUtil.minValue(DecimalUtil.MAX_DECIMAL32_PRECISION, 2)),
            //    KuduPredicate.None(decimal32Col));
            //Assert.Equal(KuduPredicate.NewComparisonPredicate(decimal64Col, ComparisonOp.Less,
            //    DecimalUtil.minValue(DecimalUtil.MAX_DECIMAL64_PRECISION, 2)),
            //    KuduPredicate.None(decimal64Col));
            //Assert.Equal(KuduPredicate.NewComparisonPredicate(decimal128Col, ComparisonOp.Less,
            //    DecimalUtil.minValue(DecimalUtil.MAX_DECIMAL128_PRECISION, 2)),
            //    KuduPredicate.None(decimal128Col));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Less, ""),
                         KuduPredicate.None(stringCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.Less, new byte[] { }),
                         KuduPredicate.None(binaryCol));
        }

        [Fact]
        public void TestGreaterEqual()
        {
            Assert.Equal(KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.GreaterEqual, sbyte.MinValue),
                         KuduPredicate.NewIsNotNullPredicate(byteCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.GreaterEqual, short.MinValue),
                         KuduPredicate.NewIsNotNullPredicate(shortCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, int.MinValue),
                         KuduPredicate.NewIsNotNullPredicate(intCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.GreaterEqual, long.MinValue),
                         KuduPredicate.NewIsNotNullPredicate(longCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.GreaterEqual, float.NegativeInfinity),
                         KuduPredicate.NewIsNotNullPredicate(floatCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.GreaterEqual, double.NegativeInfinity),
                         KuduPredicate.NewIsNotNullPredicate(doubleCol));
            //Assert.Equal(KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.GreaterEqual,
            //    DecimalUtil.minValue(DecimalUtil.MAX_DECIMAL32_PRECISION, 2)),
            //    KuduPredicate.NewIsNotNullPredicate(decimal32Col));
            //Assert.Equal(KuduPredicate.NewComparisonPredicate(decimal64Col, ComparisonOp.GreaterEqual,
            //    DecimalUtil.minValue(DecimalUtil.MAX_DECIMAL64_PRECISION, 2)),
            //    KuduPredicate.NewIsNotNullPredicate(decimal64Col));
            //Assert.Equal(KuduPredicate.NewComparisonPredicate(decimal128Col, ComparisonOp.GreaterEqual,
            //    DecimalUtil.minValue(DecimalUtil.MAX_DECIMAL128_PRECISION, 2)),
            //    KuduPredicate.NewIsNotNullPredicate(decimal128Col));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.GreaterEqual, ""),
                         KuduPredicate.NewIsNotNullPredicate(stringCol));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(binaryCol, ComparisonOp.GreaterEqual, new byte[] { }),
                         KuduPredicate.NewIsNotNullPredicate(binaryCol));

            Assert.Equal(KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.GreaterEqual, sbyte.MaxValue),
                         KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.Equal, sbyte.MaxValue));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.GreaterEqual, short.MaxValue),
                         KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.Equal, short.MaxValue));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.GreaterEqual, int.MaxValue),
                         KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, int.MaxValue));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.GreaterEqual, long.MaxValue),
                         KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.Equal, long.MaxValue));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.GreaterEqual, float.PositiveInfinity),
                         KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Equal, float.PositiveInfinity));
            Assert.Equal(KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.GreaterEqual, double.PositiveInfinity),
                         KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Equal, double.PositiveInfinity));
        }

        [Fact]
        public void TestToString()
        {
            Assert.Equal("`bool` = True",
                         KuduPredicate.NewComparisonPredicate(boolCol, ComparisonOp.Equal, true).ToString());
            Assert.Equal("`byte` = 11",
                         KuduPredicate.NewComparisonPredicate(byteCol, ComparisonOp.Equal, 11).ToString());
            Assert.Equal("`short` = 11",
                         KuduPredicate.NewComparisonPredicate(shortCol, ComparisonOp.Equal, 11).ToString());
            Assert.Equal("`int` = -123",
                         KuduPredicate.NewComparisonPredicate(intCol, ComparisonOp.Equal, -123).ToString());
            Assert.Equal("`long` = 5454",
                         KuduPredicate.NewComparisonPredicate(longCol, ComparisonOp.Equal, 5454).ToString());
            Assert.Equal("`float` = 123.456",
                         KuduPredicate.NewComparisonPredicate(floatCol, ComparisonOp.Equal, 123.456f).ToString());
            Assert.Equal("`double` = 123.456",
                         KuduPredicate.NewComparisonPredicate(doubleCol, ComparisonOp.Equal, 123.456).ToString());
            //Assert.Equal("`decimal32` = 123.45",
            //    KuduPredicate.NewComparisonPredicate(decimal32Col, ComparisonOp.Equal,
            //        BigDecimal.valueOf(12345, 2)).toString());
            //Assert.Equal("`decimal64` = 123456789.10",
            //    KuduPredicate.NewComparisonPredicate(decimal64Col, ComparisonOp.Equal,
            //        BigDecimal.valueOf(12345678910L, 2)).toString());
            //Assert.Equal("`decimal128` = 1234567891011121314.15",
            //    KuduPredicate.NewComparisonPredicate(decimal128Col, ComparisonOp.Equal,
            //        new BigDecimal("1234567891011121314.15")).toString());
            Assert.Equal("`string` = \"my string\"",
                         KuduPredicate.NewComparisonPredicate(stringCol, ComparisonOp.Equal, "my string").ToString());
            Assert.Equal("`binary` = AB-01-CD", KuduPredicate.NewComparisonPredicate(
                binaryCol, ComparisonOp.Equal, new byte[] { 0xAB, 0x01, 0xCD }).ToString());
            Assert.Equal("`int` IN (-10, 0, 10)",
                         IntInList(10, 0, -10).ToString());
            Assert.Equal("`string` IS NOT NULL",
                         KuduPredicate.NewIsNotNullPredicate(stringCol).ToString());
            Assert.Equal("`string` IS NULL",
                         KuduPredicate.NewIsNullPredicate(stringCol).ToString());
            // IS NULL predicate on non-nullable column = NONE predicate
            Assert.Equal("`int` NONE",
                    KuduPredicate.NewIsNullPredicate(intCol).ToString());

            Assert.Equal("`bool` = True", KuduPredicate.NewInListPredicate(
                boolCol, new[] { true }).ToString());
            Assert.Equal("`bool` = False", KuduPredicate.NewInListPredicate(
                boolCol, new[] { false }).ToString());
            Assert.Equal("`bool` IS NOT NULL", KuduPredicate.NewInListPredicate(
                boolCol, new[] { false, true, true }).ToString());
            Assert.Equal("`byte` IN (1, 10, 100)", KuduPredicate.NewInListPredicate(
                byteCol, new byte[] { 1, 10, 100 }).ToString());
            Assert.Equal("`short` IN (1, 10, 100)", KuduPredicate.NewInListPredicate(
                shortCol, new short[] { 1, 100, 10 }).ToString());
            Assert.Equal("`int` IN (1, 10, 100)", KuduPredicate.NewInListPredicate(
                intCol, new int[] { 1, 100, 10 }).ToString());
            Assert.Equal("`long` IN (1, 10, 100)", KuduPredicate.NewInListPredicate(
                longCol, new long[] { 1, 100, 10 }).ToString());
            Assert.Equal("`float` IN (78.9, 123.456)", KuduPredicate.NewInListPredicate(
                floatCol, new float[] { 123.456f, 78.9f }).ToString());
            Assert.Equal("`double` IN (78.9, 123.456)", KuduPredicate.NewInListPredicate(
                doubleCol, new double[] { 123.456d, 78.9d }).ToString());
            Assert.Equal("`string` IN (\"a\", \"my string\")",
                         KuduPredicate.NewInListPredicate(stringCol, new string[] { "my string", "a" }).ToString());
            Assert.Equal("`binary` IN (00, AB-01-CD)", KuduPredicate.NewInListPredicate(
                binaryCol, new byte[][] { new byte[] { 0xAB, 0x01, 0xCD }, new byte[] { 0x00 } }).ToString());
        }

        //[Fact]
        //public void TestDecimalCoercion()
        //{
        //    Assert.assertEquals(
        //        KuduPredicate.NewComparisonPredicate(decimal32Col, LESS, BigDecimal.valueOf(123)),
        //        KuduPredicate.NewComparisonPredicate(decimal32Col, LESS, BigDecimal.valueOf(12300, 2))
        //    );
        //    Assert.assertEquals(
        //        KuduPredicate.NewComparisonPredicate(decimal32Col, GREATER, BigDecimal.valueOf(123, 1)),
        //        KuduPredicate.NewComparisonPredicate(decimal32Col, GREATER, BigDecimal.valueOf(1230, 2))
        //    );
        //    Assert.assertEquals(
        //        KuduPredicate.NewComparisonPredicate(decimal32Col, EQUAL, BigDecimal.valueOf(1, 0)),
        //        KuduPredicate.NewComparisonPredicate(decimal32Col, EQUAL, BigDecimal.valueOf(100, 2))
        //    );
        //}

        private void TestMerge(KuduPredicate a, KuduPredicate b, KuduPredicate expected)
        {
            Assert.Equal(expected, a.Merge(b));
            Assert.Equal(expected, b.Merge(a));
        }

        private KuduPredicate IntRange(int lower, int upper)
        {
            //Preconditions.checkArgument(lower < upper);
            // TODO: Use KuduEncoder
            var kLower = new byte[4];
            var kUpper = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(kLower, lower);
            BinaryPrimitives.WriteInt32LittleEndian(kUpper, upper);
            return new KuduPredicate(PredicateType.Range, intCol, kLower, kUpper);
        }

        private KuduPredicate BoolInList(params bool[] values) =>
            KuduPredicate.NewInListPredicate(boolCol, values);

        private KuduPredicate IntInList(params int[] values) =>
            KuduPredicate.NewInListPredicate(intCol, values);

        private KuduPredicate StringInList(params string[] values) =>
            KuduPredicate.NewInListPredicate(stringCol, values);

        private static ColumnSchema CreateColumnSchema(string name, DataType dataType) =>
            new ColumnSchema(
                name,
                dataType,
                false,
                dataType == DataType.String,
                EncodingType.AutoEncoding,
                CompressionType.DefaultCompression,
                null);
    }
}