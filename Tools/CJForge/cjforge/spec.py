"""Validated read access to recipe JSON. Unknown keys are reported so typos never pass silently."""

from __future__ import annotations

import difflib

from .errors import ForgeError

MISSING = object()


class Spec:
    def __init__(self, data, path: str = "$", topic: str | None = None):
        if not isinstance(data, dict):
            raise ForgeError("object expected", path)
        self._data = data
        self._used: set[str] = set()
        self.path = path
        self.topic = topic

    def at(self, key) -> str:
        return f"{self.path}[{key}]" if isinstance(key, int) else f"{self.path}.{key}"

    def has(self, key: str) -> bool:
        return key in self._data

    def keys(self) -> list[str]:
        return [k for k in self._data if not k.startswith("_")]

    def raw(self, key: str, default=MISSING):
        self._used.add(key)
        if key in self._data:
            return self._data[key]
        if default is MISSING:
            raise ForgeError(f"'{key}' is required", self.at(key), self._hint())
        return default

    def num(self, key: str, default=MISSING, lo: float | None = None, hi: float | None = None) -> float:
        value = self.raw(key, default)
        if value is None:
            return None
        return check_num(value, self.at(key), lo, hi)

    def int(self, key: str, default=MISSING, lo: int | None = None, hi: int | None = None) -> int:
        value = self.raw(key, default)
        if isinstance(value, bool) or not isinstance(value, int):
            if isinstance(value, float) and value.is_integer():
                value = int(value)
            else:
                raise ForgeError("integer expected", self.at(key))
        check_num(value, self.at(key), lo, hi)
        return value

    def str(self, key: str, default=MISSING, choices=None) -> str:
        value = self.raw(key, default)
        if value is None:
            return None
        if not isinstance(value, str):
            raise ForgeError("string expected", self.at(key))
        if choices is not None and value not in choices:
            raise ForgeError(f"'{value}' is not one of {sorted(choices)}", self.at(key))
        return value

    def bool(self, key: str, default=MISSING) -> bool:
        value = self.raw(key, default)
        if not isinstance(value, bool):
            raise ForgeError("true/false expected", self.at(key))
        return value

    def obj(self, key: str, default=MISSING, topic: str | None = None) -> Spec | None:
        value = self.raw(key, default)
        if value is None:
            return None
        return Spec(value, self.at(key), topic or self.topic)

    def list(self, key: str, default=MISSING) -> list:
        value = self.raw(key, default)
        if value is None:
            return None
        if not isinstance(value, list):
            raise ForgeError("array expected", self.at(key))
        return value

    def objs(self, key: str, default=MISSING, topic: str | None = None) -> list[Spec]:
        items = self.list(key, default)
        if items is None:
            return []
        return [Spec(item, f"{self.at(key)}[{i}]", topic or self.topic) for i, item in enumerate(items)]

    def finish(self) -> None:
        unknown = [k for k in self._data if k not in self._used and not k.startswith("_")]
        if unknown:
            guesses = [f"{k}->{m[0]}" for k in unknown if (m := difflib.get_close_matches(k, self._used, 1, 0.6))]
            hint = f"did you mean {', '.join(guesses)}? " if guesses else ""
            raise ForgeError(f"unknown key(s): {', '.join(unknown)}", self.path,
                             (hint + (self._hint() or "")).strip() or None)

    def _hint(self) -> str | None:
        return f"run `cjforge ref {self.topic}`" if self.topic else None


def check_num(value, at: str, lo=None, hi=None) -> float:
    if isinstance(value, bool) or not isinstance(value, (int, float)):
        raise ForgeError("number expected", at)
    if lo is not None and value < lo:
        raise ForgeError(f"must be >= {lo} (got {value})", at)
    if hi is not None and value > hi:
        raise ForgeError(f"must be <= {hi} (got {value})", at)
    return float(value)
