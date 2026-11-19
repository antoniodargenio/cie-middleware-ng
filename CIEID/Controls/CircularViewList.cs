using CIEID.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CIEID.Controls
{
    /// <summary>Lista circolare di CIE: lo scorrimento sposta gli elementi da un'estremita' all'altra (usata dal carosello).</summary>
    class CircularViewList : List<CieModel>
    {
        public CircularViewList()
        { }

        public CircularViewList(IEnumerable<CieModel> controls)
        {
            AddRange(controls);
        }

        // Sposta il primo elemento in coda
        public void ShiftLeft()
        {
            var model = this.FirstOrDefault();

            if (model == null) return;

            this.Remove(model);
            this.Add(model);
        }

        // Sposta l'ultimo elemento in testa
        public void ShiftRight()
        {
            var model = this.LastOrDefault();

            if (model == null) return;

            this.Remove(model);
            this.Insert(0, model);
        }
    }
}
