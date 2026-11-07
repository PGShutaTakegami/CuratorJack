class ForgeError(Exception):
    """Recipe / generation error. `at` is a JSONPath-like location inside the recipe."""

    def __init__(self, message: str, at: str = "", hint: str | None = None):
        super().__init__(message)
        self.message = message
        self.at = at
        self.hint = hint

    def to_dict(self) -> dict:
        result = {"error": self.message}
        if self.at:
            result["at"] = self.at
        if self.hint:
            result["hint"] = self.hint
        return result
