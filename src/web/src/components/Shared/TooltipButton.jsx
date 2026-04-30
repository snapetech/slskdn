import React from 'react';
import { Button, Popup } from 'semantic-ui-react';

const getText = (children) => {
  if (typeof children === 'string') {
    return children;
  }

  if (Array.isArray(children)) {
    return children.filter((child) => typeof child === 'string').join(' ').trim();
  }

  return '';
};

const TooltipButton = ({
  'aria-label': ariaLabel,
  children,
  popupPosition = 'top center',
  title,
  tooltip,
  ...props
}) => {
  const inferredLabel = ariaLabel || title || getText(children) || undefined;
  const button = (
    <Button
      aria-label={ariaLabel || inferredLabel}
      title={title}
      {...props}
    >
      {children}
    </Button>
  );
  const content = tooltip || title || inferredLabel;

  if (!content) {
    return button;
  }

  return (
    <Popup
      content={content}
      position={popupPosition}
      trigger={button}
    />
  );
};

TooltipButton.Group = Button.Group;
TooltipButton.Or = Button.Or;

export default TooltipButton;
