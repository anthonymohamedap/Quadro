using QuadroApp.Model.DB;



using System;
using System.Collections.Generic;

namespace QuadroApp.ViewModels
{
    public class CalendarDay
    {
        public DateTime Date { get; set; }
        public bool IsSelected { get; set; }
        public List<WerkTaak> Taken { get; set; } = new();

        public int BezettingMinuten { get; set; }

        public bool IsCurrentMonth { get; set; }

        public int MaxCapaciteitMinuten { get; set; } = 480; // 8u

        public double CapaciteitRatio =>
            MaxCapaciteitMinuten == 0
                ? 0
                : (double)BezettingMinuten / MaxCapaciteitMinuten;
    }
}

