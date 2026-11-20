namespace FinGuard.Compliance.Domain.Specifications;

/// <summary>An in-memory domain predicate (D-174). Never translated to EF expressions.</summary>
public abstract class Specification<T>
{
    public abstract bool IsSatisfiedBy(T candidate);

    public Specification<T> And(Specification<T> other) => new AndSpecification(this, other);

    public Specification<T> Not() => new NotSpecification(this);

    private sealed class AndSpecification(Specification<T> left, Specification<T> right) : Specification<T>
    {
        public override bool IsSatisfiedBy(T candidate) => left.IsSatisfiedBy(candidate) && right.IsSatisfiedBy(candidate);
    }

    private sealed class NotSpecification(Specification<T> inner) : Specification<T>
    {
        public override bool IsSatisfiedBy(T candidate) => !inner.IsSatisfiedBy(candidate);
    }
}
