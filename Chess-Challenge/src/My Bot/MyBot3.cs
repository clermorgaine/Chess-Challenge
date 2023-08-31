using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class MyBot3 : IChessBot
{

    public Move Think(Board board, Timer timer)
    {
        currentBoard = board;
        TurnTimer = timer;

        TimeAllotted = (timer.MillisecondsRemaining / 10);

        int depth = 0;
        do
        {
            Search(depth, int.MinValue, int.MaxValue);
            depth++;
        } while (!SearchCancelled());
        transpositionTable.TryGetValue(out TranspositionValue value);
        return value.BestMove;
    }

    Timer TurnTimer;

    int TimeAllotted;

    static Board currentBoard;

    static TranspositionTable transpositionTable = new();

    bool SearchCancelled() => TurnTimer.MillisecondsElapsedThisTurn > TimeAllotted;

    int Search(int depth, int alpha, int beta)
    {
        
        if (depth == 0 || currentBoard.IsInCheck() || currentBoard.IsDraw())
        {
            transpositionTable.Add(new(true, depth, Heuristic(), Move.NullMove));
            return Heuristic();
        }
        TranspositionValue value = new(false, depth, int.MinValue, Move.NullMove);
        if (transpositionTable.TryGetValue(out value) && value.Exact && value.Depth >= depth) return value.Value;

        PriorityQueue<Move, Move> Moves = new(new MoveComparer());
        Moves.EnqueueRange(currentBoard.GetLegalMoves().Select(m => (m, m)));
        Move BestMove = Move.NullMove;
        while (Moves.Count > 0)
        {
            Move move = Moves.Dequeue();
            currentBoard.MakeMove(move);
            value.Update(new(false, depth, -Search(depth - 1, -beta, -alpha), move));
            currentBoard.UndoMove(move);

            if (SearchCancelled())
            {
                transpositionTable.Add(value);
                return value.Value;
            }

            alpha = Math.Max(alpha, value.Value);
            if (alpha >= beta)
            {
                break;
            }
        }
        value.Exact = (alpha <= beta);
        transpositionTable.Add(value);
        return value.Value;
    }

    int Heuristic()
    {
        if (currentBoard.IsInCheckmate()) return int.MinValue;
        if (currentBoard.IsDraw()) return 0;

        return currentBoard.GetAllPieceLists().Sum(PieceValue) * Color(currentBoard.IsWhiteToMove);

    }

    static int Color(bool isWhite) => isWhite ? 1 : -1;

    static int[] _PieceValue = new int[] { 0, 1, 3, 3, 5, 9, 0 };

    static int PieceValue(PieceType pieceType) => _PieceValue[(int)pieceType];

    static int PieceValue(PieceList pieces) => pieces.Count * PieceValue(pieces.TypeOfPieceInList) * Color(pieces.IsWhitePieceList);

    class MoveComparer : IComparer<Move>
    {
        public MoveComparer()
        {
            BestMove = transpositionTable.TryGetValue(out TranspositionValue tv) ? tv.BestMove : Move.NullMove;
        }

        Move BestMove;

        public int Compare(Move x, Move y)
        {
            if (BestMove.Equals(x)) return -1;
            if (BestMove.Equals(y)) return 1;

            if (x.IsPromotion && x.StartSquare == y.StartSquare && x.TargetSquare == y.TargetSquare) return PieceValue(y.PromotionPieceType) - PieceValue(x.PromotionPieceType);

            return Heat(y.StartSquare) - Heat(x.StartSquare) + Heat(y.TargetSquare) - Heat(x.TargetSquare);
        }

        int Heat(Square square)
        {
            throw new NotImplementedException();
        }
    }

    class TranspositionTable
    {
        Dictionary<ulong, TranspositionValue> Table = new();
        Queue<ulong> RemovalQueue = new();
        int MaxCapacity = 100;
        public void Add(TranspositionValue value)
        {
            ulong key = currentBoard.ZobristKey;
            if (!Table.ContainsKey(key))
            {
                if (Table.Count == MaxCapacity)
                {
                    ulong KeyToRemove;
                    do
                    {
                        KeyToRemove = RemovalQueue.Dequeue();
                    } while (RemovalQueue.Any(k => k.Equals(KeyToRemove)));
                    Table.Remove(KeyToRemove);
                }
                Table.Add(key, value);
            }
            else Table[key].Update(value);
            RemovalQueue.Enqueue(key);
        }

        public bool TryGetValue(out TranspositionValue value) => Table.TryGetValue(currentBoard.ZobristKey, out value);
    }

    struct TranspositionValue
    {
        public TranspositionValue(bool exact, int depth, int value, Move bestMove)
        {
            Exact = exact;
            Depth = depth;
            Value = value;
            BestMove = bestMove;
        }

        public bool Exact;
        public int Depth;
        public int Value;
        public Move BestMove;

        public void Update(TranspositionValue NewValue)
        {
            if (NewValue.Depth > Depth || NewValue.Depth == Depth && NewValue.Value > Value)
            {
                Exact = NewValue.Exact;
                Depth = NewValue.Depth;
                Value = NewValue.Value;
                if (!NewValue.BestMove.IsNull) BestMove = NewValue.BestMove;
            }
        }
    }
}

