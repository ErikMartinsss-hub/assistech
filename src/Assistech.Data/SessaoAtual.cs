using Assistech.Shared.Enums;

namespace Assistech.Data;

/// <summary>Contexto do usuario autenticado, preenchido a cada requisicao pela API.</summary>
public sealed class SessaoAtual
{
    public Guid EmpresaId { get; set; }
    public Guid UsuarioId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Tecnico;
    public string EmpresaNome { get; set; } = string.Empty;
    public string EmpresaCorPrimaria { get; set; } = "#0d6efd";

    public bool EhAdmin => Perfil == PerfilUsuario.Admin;
}
