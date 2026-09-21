using Microsoft.AspNetCore.Identity;

namespace PTAPControl.Data;

/// <summary>
/// Estado de aprobación del usuario por el administrador.
/// </summary>
public enum ApprovalStatus
{
    Pending,   // Esperando aprobación del administrador
    Approved,  // Acceso concedido
    Rejected   // Acceso denegado
}

/// <summary>
/// Usuario extendido de HIDROCONTROL PTAP.
/// Añade nombre completo, empresa y estado de aprobación al IdentityUser base.
/// </summary>
public class ApplicationUser : IdentityUser
{
    /// <summary>Nombre completo del usuario (requerido en el registro).</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Empresa u organización del usuario (opcional).</summary>
    public string? Company { get; set; }

    /// <summary>Estado de aprobación por el administrador.</summary>
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Pending;

    /// <summary>Fecha en que el administrador procesó la solicitud.</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>Notas del administrador sobre el usuario.</summary>
    public string? AdminNotes { get; set; }

    /// <summary>Fecha de registro.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Último acceso al sistema.</summary>
    public DateTime? LastLoginAt { get; set; }
}
