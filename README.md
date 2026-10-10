# SerialPortLocator
Assists in locating the physical serial port with the OS identification designator (e.g., COM port, /dev/tty…) with the use of a loopback adapter.

## Usage
SerialPortLocator [options]

### CLI Options
| Option | Description |
| --- | --- |
| <code>&#8209;&#8209;ports&#8209;to&#8209;ignore&nbsp;<"CSV"></code> | A comma (`','`) separated values (CSV) string of ports to ignore while locating. Use the backslash (`'\'`) character to escape comma or backslash characters that are to be preserved. For example, `"COM3,COM4"`. |
| <code>&#8209;&#8209;ports&#8209;to&#8209;test&nbsp;<"CSV"></code> | A comma (`','`) separated values (CSV) string of ports to test. The default behavior is to to query for a list of valid serial port names. Use the backslash (`'\'`) character to escape comma or backslash characters that are to be preserved. For example, `"COM3,COM4,COM6,COM7"`. |
| <code>&#8209;&#8209;retry&#8209;ports&#8209;on&#8209;exception</code> | When a port encounters an exception, it will be retried. The default behavior is to ignore them after they encounter an exception. |
| <code>&#8209;&#8209;retry&#8209;ports&#8209;on&#8209;locate</code> | When a port is located, it will continue to be located. The default behavior is to ignore it after it is located. |
| <code>&#8209;&#8209;original&#8209;order&#8209;of&#8209;port&#8209;names</code> | Keep the original order of the port names. The default behavior is to sort the port names. |
| <code>&#8209;&#8209;list&#8209;port&#8209;names</code> | Lists the currently available port names, each on a separate line and in the original order. |
| <code>&#8209;?, &#8209;h, &#8209;&#8209;help</code> | Show help and usage information |
| <code>&#8209;&#8209;version</code> | Show version information |

## Loopback Adapters
A loopback adapter connects the transmit pin(s) to the receive pin(s) so that when the test writes on the port it could be read from the port. For example, for an RS-232 DE9M port, pins 2 and 3 would be connected together. Some serial ports may not allow for a loopback adapter (e.g., [RS-485](https://industrialmonitordirect.com/blogs/knowledgebase/testing-rs-485-two-wire-half-duplex-loopback-methods#section-0)).
