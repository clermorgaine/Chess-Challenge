using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    static int MaxEval = 500000;

    
    public Move Think(Board board, Timer timer)
    {
        
        if (firstTurn && board.IsWhiteToMove) Console.WriteLine(string.Format(@"[FEN ""{0}""]", board.GameStartFenString.Trim()));

        firstTurn = false;

        currentBoard = board;
        TurnTimer = timer;

        TimeAllotted = (timer.MillisecondsRemaining - 5000) / 10;

        int depth = 2;
        TranspositionEntry value =Search(depth, -MaxEval, MaxEval);
        while (!SearchCancelled() && value.Value < MaxEval)
        {
            depth += 2;
            TranspositionEntry newValue = Search(depth, -MaxEval, MaxEval);
            if (!SearchCancelled()) value = newValue;
        }
        Console.WriteLine(MoveName(value.BestMove) + "{Depth: " + (float)value.Depth / 2 + ", Eval: " + value.Value * Color(currentBoard.IsWhiteToMove) +"}");
        return value.BestMove;
    }

    


    Timer TurnTimer;

    int TimeAllotted;

    static Board currentBoard;

    TranspositionTable transpositionTable = new();

    bool SearchCancelled() => TurnTimer.MillisecondsElapsedThisTurn > TimeAllotted;

    TranspositionEntry Search(int depth, int alpha, int beta)
    {
        if (depth == 0 || currentBoard.IsInCheckmate() || currentBoard.IsDraw()) return new (Move.NullMove, Heuristic(),  0);

        SortedSet<Move> Moves = new(currentBoard.GetLegalMoves(), new MoveComparer(transpositionTable));
        Move BestMove = Moves.First();
        foreach (Move move in Moves)
        {
            currentBoard.MakeMove(move);
            int value = -Search(depth - 2, -beta, -alpha).Value;
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
        transpositionTable.Add(new(BestMove, alpha, depth));
        return new(BestMove, alpha, depth);
    }

    int Heuristic()
    {
        if (currentBoard.IsInCheckmate()) return -MaxEval;
        if (currentBoard.IsDraw()) return 0;

        //if (!immediate && currentBoard.GetLegalMoves(true).Length > 0) return Search(-1, -MaxEval, MaxEval);
        
        int value = currentBoard.GetAllPieceLists().Sum(PieceValue);
        
        int winningPlayer = Math.Sign(value);
        int material = currentBoard.GetAllPieceLists().Sum(p => Math.Abs(PieceValue(p)));

        if (value != 0 && material <= 4600)
        {
            Square LoserKingSquare = currentBoard.GetKingSquare(winningPlayer == -1);
            //Square WinnerKingSquare = currentBoard.GetKingSquare(winningPlayer == 1);
            //value -= Math.Abs(LoserKingSquare.File-WinnerKingSquare.File) + Math.Abs(LoserKingSquare.Rank - WinnerKingSquare.Rank);
            value += (50 //currentBoard.GetPieceList(PieceType.Pawn, winningPlayer == 1).Select(p => winningPlayer == 1 ? p.Square.Rank : 7 - p.Square.Rank).Sum()
            - (Math.Min(LoserKingSquare.File, 7 - LoserKingSquare.File) + Math.Min(LoserKingSquare.Rank, 7 - LoserKingSquare.Rank))) * winningPlayer;
        }
        
        return value * Color(currentBoard.IsWhiteToMove);
        
    }

    static int Color(bool isWhite) => isWhite ? 1 : -1;

    static int[] _PieceValue = new int[] { 0, 100, 300, 300, 500, 900, 1000 };
    
    static string[] PieceName = new string[] { "", "", "N", "B", "R", "Q", "K" };
    
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

    class MoveComparer : IComparer<Move>
    {
        public MoveComparer(TranspositionTable transpositionTable)
        {
            BestMove = transpositionTable.GetMove();

            /*int value = currentBoard.GetAllPieceLists().Sum(PieceValue) * Color(currentBoard.IsWhiteToMove);
            int material = currentBoard.GetAllPieceLists().Sum(p => Math.Abs(PieceValue(p)));

            EndGame = (value > 500 || (value > 0 && material <= 4600));
            TargetSquare = currentBoard.GetKingSquare(!currentBoard.IsWhiteToMove);*/
        }

        Move BestMove;
        //bool EndGame;
        //Square TargetSquare;

        public int Compare(Move x, Move y)
        {
            if (BestMove.Equals(x)) return -1;
            if (BestMove.Equals(y)) return 1;

            int value = PieceValue(y.PromotionPieceType) - PieceValue(x.PromotionPieceType) + PieceValue(y.CapturePieceType) - PieceValue(x.CapturePieceType);
               // + PieceValue(x.MovePieceType) - PieceValue(y.MovePieceType);
            if (value != 0) return value;
            value = Heat(y.TargetSquare) - Heat(x.TargetSquare);
            if (value != 0) return value; 
            return Heat(x.StartSquare) - Heat(y.StartSquare);
        }

        int Heat(Square square)
        {
            // if (EndGame) return -(Math.Abs(TargetSquare.File - square.File) + Math.Abs(TargetSquare.Rank - square.Rank));
            return Math.Min(square.File, 7 - square.File) + Math.Min(square.Rank, 7 - square.Rank);
        }
    }

    class TranspositionTable
    {
        Dictionary<ulong, TranspositionEntry> Table1 = new();
        Dictionary<ulong , TranspositionEntry> Table2 = new();
        int MaxCapacity = 6710886;
        public void Add(TranspositionEntry entry)
        {
            if (Table2.ContainsKey(key)) Table1.TryAdd(key, Table2[key]);
            if (!Table1.TryAdd(key, entry) && Table1[key].Replace(entry)) Table1[key] = entry;
            if (Table1.Count > MaxCapacity)
            {
                Table2 = Table1;
                Table1 = new();
            }
        }

        public Move GetMove()
        {
            if (Table1.ContainsKey(key)) return Table1[key].BestMove;
            if (Table2.ContainsKey(key))
            {
                TranspositionEntry result = Table2[key];
                Add(result);
                return result.BestMove;
            }
            return Move.NullMove;
        }

        ulong key => currentBoard.ZobristKey;
    }


    struct TranspositionEntry
    {
        public TranspositionEntry (Move bestMove, int value, int depth)
        {
            BestMove = bestMove;
            Depth = depth;
            Value = value;
        }

        public Move BestMove;
        public int Depth;
        public int Value;

        public bool Replace(TranspositionEntry NewEntry) => NewEntry.Depth > Depth || (NewEntry.Depth == Depth && NewEntry.Value > Value);
 
    }

}

