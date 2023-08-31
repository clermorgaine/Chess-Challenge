using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class MyBot1 : IChessBot
{

    public Move Think(Board board, Timer timer)
    {
        currentBoard = board;
        TurnTimer = timer;

        TimeAllotted = (timer.MillisecondsRemaining / 10);

        int depth = 0;
        PositionValue value = new(0, currentBoard.GetLegalMoves().First());
        PositionValue newValue;
        while(true)
        {
            newValue = Search(depth, int.MinValue, int.MaxValue);
            depth++;
            if (SearchCancelled()) break;
            value = newValue;
        }
        return value.BestMove;
    }

    Timer TurnTimer;

    int TimeAllotted;

    static Board currentBoard;

    bool SearchCancelled() => TurnTimer.MillisecondsElapsedThisTurn > TimeAllotted;

    PositionValue Search(int depth, int alpha, int beta)
    {
        if (depth == 0 || currentBoard.IsInCheck() || currentBoard.IsDraw())
        {
            return new(Heuristic(), currentBoard.GetLegalMoves().FirstOrDefault(Move.NullMove));
        }
        int value = int.MinValue;
        PriorityQueue<Move, Move> Moves = new(new MoveComparer());
        Moves.EnqueueRange(currentBoard.GetLegalMoves().Select(m => (m, m)));
        Move BestMove = Move.NullMove;
        while (Moves.Count > 0)
        {           
            Move move = Moves.Dequeue();
            currentBoard.MakeMove(move);
            int NewValue = -Search(depth - 1, -beta, -alpha).Value;
            if (NewValue >= value)
            {
                value = NewValue;
                BestMove = move;
            }
            
            currentBoard.UndoMove(move);

            if (SearchCancelled()) return new(value, BestMove);

            alpha = Math.Max(alpha, value);
            if (alpha >= beta)
            {
                break;
            }
        }
        return new(value, BestMove);
    }

    int Heuristic()
    {
        if (currentBoard.IsInCheckmate()) return int.MinValue;
        if (currentBoard.IsDraw()) return 0;

        return currentBoard.GetAllPieceLists().Sum(PieceValue) * Color(currentBoard.IsWhiteToMove);

    }

    static int Color(bool isWhite) => isWhite ? 1 : -1;

    static int[] _PieceValue = new int[] { 0, 1, 3, 3, 5, 9, 0 };

    static int PieceValue(PieceType pieceType) => _PieceValue[(int) pieceType];

    static int PieceValue(PieceList pieces) => pieces.Count * PieceValue(pieces.TypeOfPieceInList) * Color(pieces.IsWhitePieceList);

    class MoveComparer : IComparer<Move>
    {
        public MoveComparer()
        {
            
        }


        public int Compare(Move x, Move y)
        {

            if (x.IsPromotion && x.StartSquare == y.StartSquare && x.TargetSquare == y.TargetSquare) return PieceValue(y.PromotionPieceType) - PieceValue(x.PromotionPieceType);
    
            return Heat(y.StartSquare) - Heat(x.StartSquare) + Heat(y.TargetSquare) - Heat(x.TargetSquare);            
        }

        int Heat(Square square)
        {
            throw new NotImplementedException();
        }
    }

    struct PositionValue
    {
        public int Value;
        public Move BestMove;

        public PositionValue(int value, Move bestMove)
        {
            Value = value;
            BestMove = bestMove;
        }
    }
}

