namespace Api_Tlapaleria.Enums
{
    public enum ExpenseCategoryType { COGS, Operativo }
    public enum AccountsPayableStatus { Pendiente, Parcial, Pagado }
    public enum PaymentScheduleStatus { Pendiente, Pagado, Vencido, Cancelada }
    public enum PaymentMethod { Efectivo, Transferencia, Tarjeta, Cheque }
    public enum ReminderChannel { Push, Email, SMS, InApp }
    public enum ReminderStatus { Pendiente, Enviada, Leida, Cancelada }
}