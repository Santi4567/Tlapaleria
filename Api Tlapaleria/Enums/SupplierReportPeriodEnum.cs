namespace Api_Tlapaleria.Enums
{
    public enum SupplierReportPeriod
    {
        CustomRange = 0, // Rango de fechas manual (startDate y endDate)
        Day = 1,         // Botón: HOY (o el día enviado en startDate)
        Week = 2,        // Botón: SEMANA (lunes a domingo de la semana actual o de startDate)
        Month = 3,       // Botón: MES (mes actual o el de startDate)
        Year = 4,        // Botón: AÑO (año actual o el de startDate)
        AllTime = 5      // Botón: TODO EL HISTORIAL (sin filtro de fechas)
    }
}