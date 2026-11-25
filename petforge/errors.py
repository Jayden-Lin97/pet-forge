class ForgeError(Exception):
    """An error safe to show to the local operator."""


class QualityError(ForgeError):
    pass


class AmbiguousRequest(ForgeError):
    """A paid write might have reached the provider. Never silently repeat it."""
