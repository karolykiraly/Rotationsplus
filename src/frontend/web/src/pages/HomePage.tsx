import { useMe } from "../useMe";

/** Overview: confirms the authenticated identity (the staff round-trip) and the caller's roles. */
export function HomePage() {
  const { user, isLoading, isError, error } = useMe();

  return (
    <>
      {isLoading && <div className="card state">Loading your profile…</div>}
      {isError && <div className="banner error" role="alert">{(error as Error).message}</div>}

      {user && (
        <div className="card">
          <dl className="dl">
            <dt>Name</dt>
            <dd>{user.name ?? "—"}</dd>
            <dt>Username</dt>
            <dd>{user.username ?? "—"}</dd>
            <dt>Roles</dt>
            <dd>
              {user.roles.length > 0
                ? user.roles.map((r) => <span key={r} className="badge" style={{ marginRight: 6 }}>{r}</span>)
                : "—"}
            </dd>
            <dt>Object ID</dt>
            <dd>{user.objectId}</dd>
            <dt>Last sign-in</dt>
            <dd>{user.lastSignInAtUtc ? new Date(user.lastSignInAtUtc).toLocaleString() : "—"}</dd>
          </dl>
        </div>
      )}
    </>
  );
}
