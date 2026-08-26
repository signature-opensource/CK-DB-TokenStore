Implements the `CK.tTokenStore` table: a generic store for opaque, expirable, revocable tokens.

A token is identified by a Scope (its functional purpose) and a Key (business data whose unicity must
be enforced in that scope, such as an email address), and the pair is unique - so the store also acts
as a one-pending-operation-per-subject guard.

The token handed out combines the identifier with a random GUID, so it cannot be guessed. Checking a
token is a write: it counts the check and postpones the expiration by a safe period (10 minutes by
default), so an operation authorized by a check cannot see its token expire midway.
