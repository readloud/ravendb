﻿namespace Corax.Queries
{
    public interface ITermProvider
    { 
        void Reset();
        bool Next(out TermMatch term);
        bool Evaluate(long id);
    }    
}