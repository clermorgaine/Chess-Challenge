using ChessChallenge.API;
using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class MyBot : IChessBot
{

    public Move Think(Board board, Timer timer)
    {

        /*if (firstTurn && board.IsWhiteToMove) Console.WriteLine(string.Format(@"[FEN ""{0}""]", board.GameStartFenString.Trim()));

        firstTurn = false; */

        currentBoard = board;
        TurnTimer = timer;

        TimeAllotted = (timer.MillisecondsRemaining - 1000) / 10;

        int depth = 0;
        do
        {
            depth++;
            Search(depth, float.NegativeInfinity, float.PositiveInfinity);
        } while (!SearchCancelled);
        //Console.WriteLine(MoveName(entry.BestMove) + "{Depth: " + (float)entry.Depth / 2 + ", Eval: " + entry.Value * Color(currentBoard.IsWhiteToMove) +"}");
        return GetTableMove();
    }

    Timer TurnTimer;
    int TimeAllotted;
    Board currentBoard;
    ulong key => currentBoard.ZobristKey;
    bool SearchCancelled => TurnTimer.MillisecondsElapsedThisTurn > TimeAllotted;

    float Search(int depth, float alpha, float beta)
    {
        if (currentBoard.IsInCheckmate()) return float.NegativeInfinity;
        if (currentBoard.IsDraw()) return 0;

        if (depth == 0)
        {
            bool[] Layer2Activations = Activation(BodyOutput(), ValueWeightsLayer1, 16 /* 16b */);
            return
                (currentBoard.GetAllPieceLists().Sum(pieces => pieces.Count * new []{ 0, 100, 300, 300, 500, 900, 0 }[(int)pieces.TypeOfPieceInList] * (pieces.IsWhitePieceList ? 1 : -1))

                + MathF.Tan(0 + new float[] { }.Where((value, index) => Layer2Activations[index]).Sum()) * 250)

                * (currentBoard.IsWhiteToMove ? 1 : -1);
        }


        Move BestMove = GetTableMove();
        IEnumerable<Move> Moves = currentBoard.GetLegalMoves().OrderBy(new MoveComparer { BestMove = BestMove
            /* HeatMap = MatrixMultiply(
                new(Activation(BodyOutput(), PolicyWeightsLayer1, 16 /* 16b *)),
                PolicyWeightsLayer2, 16 /*4c*)*/ }.MoveRating); 


        foreach (Move move in Moves)
        {
            currentBoard.MakeMove(move);
            float value = -Search(depth - 1, -beta, -alpha);
            currentBoard.UndoMove(move);
            if (alpha < value)
            {
                BestMove = move;
                alpha = value;
            }
            if (SearchCancelled)
            {
                depth = 0; break;
            }
            if (alpha >= beta) break;
        }


        TranspositionEntry entry = new() { BestMove = BestMove, Depth = depth, Value = alpha };
        if (!Table.TryAdd(key, entry) && Table[key].Replace(entry)) Table[key] = entry;
        RemovalQueue.Enqueue(key);
        while (Table.Count > 8388608)
        {
            ulong KeyToRemove = RemovalQueue.Dequeue();
            if (!RemovalQueue.Contains(KeyToRemove)) Table.Remove(KeyToRemove);
        }


        return alpha;
    }

    /* static string[] PieceName = new string[] { "", "", "N", "B", "R", "Q", "K" };
    
    bool firstTurn = true;

    static string MoveName(Move move) => move.IsNull ? "" :
        string.Format("{0} {1}{2}{3}{4}{5}{6} ",
        currentBoard.IsWhiteToMove ? (currentBoard.PlyCount/ 2 + 1).ToString() + "." : ((currentBoard.PlyCount + 1) / 2).ToString() + "...",
        PieceName[(int)move.MovePieceType],
        move.StartSquare.Name,
        move.IsCapture ? "x" : "",
        move.TargetSquare.Name,
        move.IsPromotion ? "=" : "",
        PieceName[(int)move.PromotionPieceType]);
    
    /*void ListMoves(int depth)
    {
        Move move = transpositionTable.GetMove();
        string output = MoveName(move);
        
        if (depth > 1)
        {            
            output += "(";
            output += MoveName(move);
            for (int i = 1; i < depth; i++)
            {
                currentBoard.MakeMove(move);
                move = transpositionTable.GetMove();
                output += MoveName(move);
            }
            output += ")";
        }
        Console.WriteLine(output);
    }*/

    //static int[] PieceValue = { 0, 100, 300, 300, 500, 900, 0 };

    class MoveComparer
    {
        public Move BestMove;
        public int[] HeatMap;

        public int MoveRating(Move move) => (move == BestMove ? 1000 : 0)  /* +  PieceValue[(int)move.PromotionPieceType] + PieceValue[(int)move.CapturePieceType]*/ - HeatMap[move.TargetSquare.Index] - HeatMap[move.StartSquare.Index];
    }

    Dictionary<ulong, TranspositionEntry> Table = new();
    Queue<ulong> RemovalQueue = new();

    Move GetTableMove()
    {
        if (Table.ContainsKey(key))
        {
            RemovalQueue.Enqueue(key);
            return Table[key].BestMove;
        }
        return Move.NullMove;
    }

    struct TranspositionEntry
    {
        public Move BestMove;
        public int Depth;
        public float Value;

        public bool Replace(TranspositionEntry NewEntry) => NewEntry.Depth > Depth || NewEntry.Depth == Depth && NewEntry.Value > Value;
    }

    const ulong ConvKernelMask = 0b1111000011110000111100001111;
    IEnumerable<int> ConvOutputShifts = Enumerable.Range(0, 64).Where(i => (0b1111100011111000111110001111100011111 & (ulong)1 << i) > 0);
    ulong[] BodyWeightsLayer1 = new ulong[] { }
        .SelectMany(input => new[] { 0, 4, 32, 36 }.Select(shift => ((ConvKernelMask << shift) & input) >> shift)).ToArray();
    BitArray BodyWeightsLayer2 = Unpack(new ulong[] { });
    BitArray ValueWeightsLayer1 = Unpack(new ulong[] { });
    /*BitArray PolicyWeightsLayer1 = Unpack(new ulong[] { });
    BitArray PolicyWeightsLayer2 = Unpack(new ulong[] { });*/

    static BitArray Unpack(ulong[] tokens) => new(tokens.SelectMany(BitConverter.GetBytes).ToArray());

    BitArray BodyOutput()
    {
        IEnumerable<ulong> inputs = Enumerable.Range(1, 6).SelectMany(i => new[]{ true, false }.Select(b => currentBoard.GetPieceBitboard((PieceType)i, b)));
        return new(Activation(
                    new(Enumerable.Range(0, 16 /* 4a */).SelectMany(output =>
                            ConvOutputShifts.Select(shift =>
                                inputs.Select((input, index) => BitOperations.PopCount((BodyWeightsLayer1[output * 12 /* i */ + index] << shift ^ input) & ConvKernelMask << shift))
                                .Sum() > 96 /* 8i */ ))
                    .ToArray()),
                BodyWeightsLayer2, 400 /* 100a */));
        
    }

    bool[] Activation(BitArray a, BitArray b, int width)
    {
        bool[] result = new bool[a.Count];
        a.Xor(b).CopyTo(result, 0);
        return result.Chunk(width).Select(i => i.Count(b => b))/*.ToArray();
    }

    bool[] Activation(BitArray a, BitArray b, int width) => MatrixMultiply(a, b, width)*/.Select(count => count * 2 > width).ToArray();
    }
}