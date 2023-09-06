using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

public class MyBot : IChessBot
{
    int MaxEval = 500000;
    
    public Move Think(Board board, Timer timer)
    {
        
        /*if (firstTurn && board.IsWhiteToMove) Console.WriteLine(string.Format(@"[FEN ""{0}""]", board.GameStartFenString.Trim()));

        firstTurn = false; */

        currentBoard = board;
        TurnTimer = timer;

        TimeAllotted = (timer.MillisecondsRemaining - 1000) / 10;

        int depth = 0;
        int value;
        do
        {
            depth += 2;
            value = Search(depth, -MaxEval, MaxEval);
        } while (!SearchCancelled() && value < MaxEval);
        //Console.WriteLine(MoveName(entry.BestMove) + "{Depth: " + (float)entry.Depth / 2 + ", Eval: " + entry.Value * Color(currentBoard.IsWhiteToMove) +"}");
        return transpositionTable.GetMove();
    }

    


    Timer TurnTimer;

    int TimeAllotted;

    static Board currentBoard;

    TranspositionTable transpositionTable = new();

    bool SearchCancelled() => TurnTimer.MillisecondsElapsedThisTurn > TimeAllotted;

    int Search(int depth, int alpha, int beta)
    {
        if (currentBoard.IsInCheckmate()) return -MaxEval;
        if (currentBoard.IsDraw()) return 0;

        if (depth == 0)
        {
            int value = currentBoard.GetAllPieceLists().Sum(PieceValue);
            return value * Color(currentBoard.IsWhiteToMove);
        }


        SortedSet<Move> Moves = new(currentBoard.GetLegalMoves(), new MoveComparer(transpositionTable));
        Move BestMove = Moves.First();
        foreach (Move move in Moves)
        {
            currentBoard.MakeMove(move);
            int value = -Search(depth - 2, -beta, -alpha);
            currentBoard.UndoMove(move);
            if (alpha < value)
            {
                BestMove = move;
                alpha = value;
            }
            if (SearchCancelled())
            {
                depth = 0; break;
            }
            if (alpha >= beta)
            {
                depth--; break;
            }
        }
        transpositionTable.Add(new() {BestMove = BestMove, Depth = depth, Value = alpha });
        return alpha;
    }

    static int Color(bool isWhite) => isWhite ? 1 : -1;

    static int[] _PieceValue = new[] { 0, 100, 300, 300, 500, 900, 1000 };
    
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
    
    static int PieceValue(PieceType pieceType) => _PieceValue[(int)pieceType];

    static int PieceValue(PieceList pieces) => pieces.Count * PieceValue(pieces.TypeOfPieceInList) * Color(pieces.IsWhitePieceList);

    static NNBody PolicyHead;

    class MoveComparer : IComparer<Move>
    {
        public MoveComparer(TranspositionTable transpositionTable)
        {
            BestMove = transpositionTable.GetMove();
        }
        Move BestMove;
        public int Compare(Move x, Move y)
        {
            if (BestMove.Equals(x)) return -1;
            if (BestMove.Equals(y)) return 1;

            int value = PieceValue(y.PromotionPieceType) - PieceValue(x.PromotionPieceType) + PieceValue(y.CapturePieceType) - PieceValue(x.CapturePieceType);
            if (value != 0) return value;
            return Heat(y.TargetSquare) - Heat(x.TargetSquare) + Heat(y.StartSquare) - Heat(x.StartSquare);
        }

        int Heat(Square square)
        {
            return Math.Min(square.File, 7 - square.File) + Math.Min(square.Rank, 7 - square.Rank);
        }
    }

    class TranspositionTable
    {
        Dictionary<ulong, TranspositionEntry> Table = new();
        Queue<ulong> RemovalQueue = new();
        int MaxCapacity = 13421772;
        public void Add(TranspositionEntry entry)
        {
            if (!Table.TryAdd(key, entry) && Table[key].Replace(entry)) Table[key] = entry;
            RemovalQueue.Enqueue(key);
            while (Table.Count > MaxCapacity)
            {
                ulong KeyToRemove = RemovalQueue.Dequeue();
                if (!RemovalQueue.Contains(KeyToRemove)) Table.Remove(KeyToRemove);
            }
        }

        public Move GetMove()
        {
            if (Table.ContainsKey(key))
            {
                RemovalQueue.Enqueue(key);
                return Table[key].BestMove;
            }
            return Move.NullMove;
        }

        ulong key => currentBoard.ZobristKey;
    }

    struct TranspositionEntry
    {
        public Move BestMove;
        public int Depth;
        public int Value;

        public bool Replace(TranspositionEntry NewEntry) => NewEntry.Depth > Depth || (NewEntry.Depth==Depth && NewEntry.Value > Value);
 
    }

    class NNBody
    {
        ulong ConvKernelMask = 0b1111000011110000111100001111;
        int[] ConvKernelShifts = new[] { 0, 4, 32, 36 };
        List<ValueTuple<PieceType, bool>> PieceColors = new();
        ulong[] kernels;
        List<List<BitArray>> layers;
        ulong[] PackedConvKernels = new ulong[32];

        public NNBody()
        {
            foreach(PieceType p in Enum.GetValues(typeof(PieceType))) 
            {
                PieceColors.Add((p, true));
                PieceColors.Add((p, false));
            }
            kernels = PackedConvKernels.SelectMany(input => ConvKernelShifts.Select(shift => ((ConvKernelMask << shift) & input) >> shift)).ToArray();
        }

       List<ulong> Segment(ulong input, int length, int count)
       {
            List<ulong> results = new();
            for (int i = 0; i < count; i++) 
            {
                results.Add(input % 1 << length);
                input = input >> length;
            }
            return results;
       }

        protected BitArray Output()
        {
            ulong[] inputs = PieceColors.Select(v => currentBoard.GetPieceBitboard(v.Item1, v.Item2)).ToArray();
            List<bool> results = new();
            int nOutputs = 32;
            for (int output = 0; output < nOutputs; output++)
            {
                for (int i = 0; i <= 32; i += 8)
                {
                    for (int shift = i; shift <= i + 4; shift++)
                    {
                        int sum = 0;
                        for (int input = 0; input < 12; input++)
                        {
                            sum += BitOperations.PopCount((kernels[output * nOutputs + input] << shift ^ inputs[input]) & ConvKernelMask << shift);
                        }
                        results.Add(sum > 96);
                    }
                }
            }
            
            
            return Outputs;
        }
    }

}

