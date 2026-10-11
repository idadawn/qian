using Microsoft.EntityFrameworkCore; public abstract class UnsafeBase : DbContext { } public sealed class IndirectContext : UnsafeBase { }
