namespace Api_Tlapaleria.Enums
{
    public enum FinancialReportPeriod
    {
        CustomRange = 0, // Rango de fechas normal (muestra días: "2026-10-13")
        Day = 1,         // Hoy (muestra horas: "09:00", "10:00")
        Month = 2,       // Mes (muestra semanas: "Semana 1", "Semana 2")
        Year = 3         // Año (muestra meses: "Ene 2026", "Feb 2026")
    }
}