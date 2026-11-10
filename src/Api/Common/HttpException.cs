namespace Api.Common;

// message renvoyé tel quel au client
public class HttpException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class BadRequestException(string message) : HttpException(StatusCodes.Status400BadRequest, message);

public class UnauthorizedException(string message) : HttpException(StatusCodes.Status401Unauthorized, message);

public class NotFoundException(string message) : HttpException(StatusCodes.Status404NotFound, message);

public class ConflictException(string message) : HttpException(StatusCodes.Status409Conflict, message);

public class PayloadTooLargeException(string message) : HttpException(StatusCodes.Status413PayloadTooLarge, message);

public class UnsupportedMediaTypeException(string message) : HttpException(StatusCodes.Status415UnsupportedMediaType, message);

public class TooManyRequestsException(string message) : HttpException(StatusCodes.Status429TooManyRequests, message);

public class BadGatewayException(string message) : HttpException(StatusCodes.Status502BadGateway, message);

public class ServiceUnavailableException(string message) : HttpException(StatusCodes.Status503ServiceUnavailable, message);

// ligne absente (lecture, update, delete)
public class RecordNotFoundException() : NotFoundException(ExceptionMiddleware.RecordNotFoundMessage);
