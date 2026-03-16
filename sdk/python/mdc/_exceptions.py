"""Exceptions raised by the MDC Python SDK."""

from __future__ import annotations


class MDCError(Exception):
    """Base class for all MDC SDK errors."""


class MDCConnectionError(MDCError):
    """Raised when the SDK cannot reach the MDC server."""


class MDCTimeoutError(MDCError):
    """Raised when a request to the MDC server times out."""


class MDCResponseError(MDCError):
    """Raised when the MDC server returns an HTTP error response.

    Attributes
    ----------
    status_code:
        HTTP status code returned by the server.
    """

    def __init__(self, status_code: int, message: str) -> None:
        self.status_code = status_code
        super().__init__(f"HTTP {status_code}: {message}")
