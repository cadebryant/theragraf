import {
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
} from "@fluentui/react-components";
import { BeakerRegular } from "@fluentui/react-icons";

interface SyntheticDataBadgeProps {
  /** When true, displays the synthetic data disclaimer banner */
  isSynthetic: boolean;
  /** Optional custom message. Defaults to standard disclaimer. */
  message?: string;
  /** Optional additional CSS class */
  className?: string;
}

/**
 * Displays a prominent disclaimer banner when viewing synthetic/demo data.
 * Only renders when isSynthetic is true.
 */
export function SyntheticDataBadge({
  isSynthetic,
  message,
  className,
}: SyntheticDataBadgeProps) {
  if (!isSynthetic) return null;

  return (
    <MessageBar intent="info" icon={<BeakerRegular />} className={className}>
      <MessageBarBody>
        <MessageBarTitle>Synthetic Demo Data</MessageBarTitle>
        {message || (
          <>
            This is synthetic demo data for testing purposes only. No real
            patient information is contained in this record.
          </>
        )}
      </MessageBarBody>
    </MessageBar>
  );
}
